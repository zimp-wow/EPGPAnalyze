using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EPGPAnalyze
{
	class Program
	{
		public class Config {
			public int    WeeklyEPMax  { get; set; } = 0;
			public int    BaseGP       { get; set; } = 150;
			public double DecayPercent { get; set; } = 0.1;
		}

		private static int EPModifier = 0;

		private static Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
		private static Mode _activeMode = Mode.Analyze;
		private static string _playerFilter = null;
		private static TrafficLogs _traffic = new TrafficLogs();
		private static Config _config = new Config();

		private enum Mode {
			Analyze = 0,
			Report  = 1,
			Both    = 2,
			Loot    = 3
		}

		private const string WelcomeStanza = @"
This file contains the analysis of EPGP values from last week to the values captured before the decay this week.
It also compares the values from before the decay to the values after the decay to ensure the decay was performed correctly.

Lines indicating potential issues will start with '!!!'.

Why do we generate this report?

EPGP values are stored in each players officer notes.  This is a similar mechanism to your public note except only people with the right access can change it.  This means they are vulnerable to tampering.
This report attempts to catch any tampering that might happen so problems can be corrected before they have an impact on loot decisions.  This tool is not able to prove if the tampering was intentional
or a side-effect of some bug in the addon we use to manage EPGP.

You can find the source for this tool at: https://github.com/zimp-wow/EPGPAnalyze

";

		static async Task Main(string[] args)
		{
			if( !File.Exists( "config.json" ) ) {
				string jsonConfig = JsonConvert.SerializeObject( new Config() );
				File.WriteAllText( "config.json", jsonConfig );
				Console.WriteLine( "Generated default config file: 'config.json" );
			}

			Console.WriteLine( WelcomeStanza );

			string configStr = File.ReadAllText( "config.json" );
			Console.WriteLine( "Using Config: " + configStr );
			_config = JsonConvert.DeserializeObject<Config>( configStr );

			if( args.Length > 0 ) {
				try {
					_activeMode = (Mode)Enum.Parse( typeof( Mode ), args[0] );
				}
				catch( Exception ) {
					Console.WriteLine( "Invalid first argument, expected either 'Analyze' or 'Report' or 'Both'" );
					return;
				}
			}

			if( args.Length > 1 ) {
				if( args[1] != "*" ) {
					_playerFilter = args[1];
				}
			}

			if( args.Length > 2 ) {
				EPModifier = int.Parse( args[2] );
				Console.WriteLine( "Using max EP modifier of: " + EPModifier );
			}

			await _traffic.ParseLogs( "CEPGP.lua" );

			if( _activeMode == Mode.Loot ) {
				if( _playerFilter == null ) {
					Console.WriteLine( "A player name argument is required for loot reports" );
					return;
				}

				ShowLoot();
				return;
			}

			SortedSet<string> sorted = new SortedSet<string>( new SortFiles() );

			string[] files = Directory.GetFiles( ".", "CCEPGP20*" );
			foreach( string file in files ) {
				sorted.Add( file );
			}

			foreach( string file in sorted ) {
				await ProcessFile( file );
			}

			Console.ReadLine();
		}

		private static async Task ProcessFile( string file ) {
			
			int year  = int.Parse( file.Substring( 8, 4 ) );
			int month = int.Parse( file.Substring( 12, 2 ) );
			int day   = int.Parse( file.Substring( 14, 2 ) );
			DateTime logDate = new DateTime( year, month, day );

			Console.WriteLine( "\nProcessing File: " + file + $" - { logDate }\n" );
			using( StreamReader sr = new StreamReader( file ) ) {
				while( !sr.EndOfStream ) {
					string line  = await sr.ReadLineAsync();

					if( line.StartsWith( "{" ) ) {
						_config = JsonConvert.DeserializeObject<Config>( line );
						Console.WriteLine( "Changing config to: " + line + "\n" );
					}

					try {
						Entry entry = new Entry( line, logDate );
						if( _entries.ContainsKey( entry.Name ) ) {
							Entry existing = _entries[ entry.Name ];

							existing.Analyze( entry );
						}

						_entries[ entry.Name ] = entry;
					}
					catch( Exception e ) {
						Console.WriteLine( "Failed to parse line: " + line + " - " + e );
					}
				}
			}
		}

		private class SortFiles : IComparer<string> {
			private Regex exp = new Regex( @".*CCEPGP([0-9]+).*", RegexOptions.Compiled );

			public int Compare( string left, string right ) {
				Match m = exp.Match( left );
				int leftInt = int.Parse( m.Groups[1].Value );

				m = exp.Match( right );
				int rightInt = int.Parse( m.Groups[1].Value );

				return leftInt.CompareTo( rightInt );
			}
		}

		private class Entry {
			public DateTime LogDate { get; set; }
			public string   Name    { get; set; }
			public string   Class   { get; set; }
			public string   Role    { get; set; }
			public int      EP      { get; set; }
			public int      GP      { get; set; }
			public double   PR      { get; set; }

			public Entry( string line, DateTime logDate ) {
				LogDate = logDate;

				string[] comps = line.Split( ',' );
				if( line.Length < 3 ) {
					throw new Exception( "Unexpected number of fields" );
				}

				Name  = string.Empty;
				// Stripping any non-ascii characters from the name, thanks Auslander
				foreach( char c in comps[0].ToCharArray() ) {
					if( c <= sbyte.MaxValue ) {
						Name += c;
					}
				}

				Class = comps[1];
				Role  = comps[2];
				EP = 0;
				GP = _config.BaseGP;
				PR = 0.0f;

				if( comps.Length < 4 ) {
					return;
				}

				if( string.IsNullOrWhiteSpace( comps[3] ) ) {
					comps[3] = "0";
				}

				EP = int.Parse( comps[3] );

				if( comps.Length < 5 ) {
					return;
				}

				if( string.IsNullOrWhiteSpace( comps[4] ) ) {
					comps[4] = $"{ _config.BaseGP }";
				}

				GP = int.Parse( comps[4] );

				if( comps.Length < 6 ) {
					return;
				}

				if( string.IsNullOrWhiteSpace( comps[5] ) ) {
					comps[5] = "0";
				}

				PR = double.Parse( comps[5] );
			}

			public void Analyze( Entry next ) {
				if( _playerFilter != null && !Name.StartsWith( _playerFilter ) ) {
					return;
				}

				int decay( int value, double percent, int baseVal = 0 ) {
					if( baseVal > 0 ) {
						return (int)Math.Max(Math.Floor( ( value - baseVal ) * ( 1.0 - percent ) + baseVal ), baseVal );
					}
					return (int)Math.Max(Math.Floor(value * ( 1.0 - percent ) ), 0 );
				}

				int decayedEP = decay( EP, _config.DecayPercent );
				int decayedEP2 = decay( decayedEP, _config.DecayPercent );
				int decayedGP = decay( GP, _config.DecayPercent, _config.BaseGP );
				int decayedGP2 = decay( decayedGP, _config.DecayPercent, 0 );

				int gpFromTraffic = 0;
				bool firstItem = true;
				List<TrafficLogs.LogEntry> traffic = _traffic.GetTrafficForPlayer( Name );
				TrafficLogs.LogEntry lastItem = null;
				foreach( var entry in traffic ) {
					if( entry.GPBefore == entry.GPAfter ) {
						continue;
					}

					if( entry.Timestamp > LogDate && entry.Timestamp < next.LogDate ) {
						if( firstItem ) {
							firstItem = false;
							int diff = Math.Abs( decayedGP - entry.GPBefore );
							if( diff > 1  && ( _activeMode == Mode.Analyze || _activeMode == Mode.Both ) ) {
								Console.WriteLine( $"\t!!! { Name } - Taking last week's GP of { GP } and decaying it they should have had { decayedGP } when they got their first item this week.  Instead they were at { entry.GPBefore } which is { decayedGP - entry.GPBefore } lower than it should be." );
							}
						}

						if( ( _activeMode == Mode.Analyze || _activeMode == Mode.Both ) && lastItem != null && lastItem.GPAfter != entry.GPBefore ) {
							Console.WriteLine( $"\t!!! { Name } - The GP recorded after the previous item does not match the GP recorded before this item was awarded.  Expected { lastItem.GPAfter } - Got { entry.GPBefore }" );
						}

						if( _activeMode == Mode.Report || _activeMode == Mode.Both ) {
							Console.WriteLine( $"\n\t\t{ Name } GP Changed [{ entry.Message } - { entry.ItemName }] GP Before { entry.GPBefore }, GP After { entry.GPAfter }\n" );
						}

						gpFromTraffic = entry.GPAfter;
						lastItem = entry;
					}
				}

				int potentialNextEP = decayedEP + _config.WeeklyEPMax + EPModifier;
				int potentialNextEP2 = decayedEP2 + _config.WeeklyEPMax + EPModifier;
				bool missedRaid = false;
				bool tooMuchEP = false;
				if( next.EP < potentialNextEP ) {
					missedRaid = true;
				}
				if( next.EP > potentialNextEP ) {
					tooMuchEP = true;
				}

				bool gotLoot = false;
				bool tooLittleGP = false;
				bool tooLittleAfterLoot = false;
				bool doubleDecay = false;
				if( next.GP > decayedGP ) {
					gotLoot = true;
				}
				if( next.GP < decayedGP ) {
					tooLittleGP = true;
				}
				if( next.GP < gpFromTraffic ) {
					tooLittleAfterLoot = true;
				}
				if( next.GP == decayedGP2 && next.GP != _config.BaseGP ) {
					doubleDecay = true;
				}

				if( _activeMode == Mode.Analyze || _activeMode == Mode.Both ) {
					if( tooMuchEP && EP != 0 ) {
						Console.WriteLine( $"\t!!! { Name } - Taking last week's EP value of { EP }, decaying it, then adding the max possible EP of { _config.WeeklyEPMax } for attending all raids this player should not have been able to go over { potentialNextEP }.  The following week shows them at { next.EP } which is { next.EP - potentialNextEP } too high." );
					}

					if( !doubleDecay && tooLittleGP && next.GP != _config.BaseGP && next.GP != 0 ) {
						Console.WriteLine( $"\t!!! { Name } - Taking last week's GP value of { GP } and decaying it they should be at { decayedGP } if they did not receive any new loot.  The following week they were at { next.GP } which is { decayedGP - next.GP } lower than it should be." );
					}

					if( tooLittleAfterLoot ) {
						Console.WriteLine( $"\t!!! { Name } - Taking the last value from logs they should be at { gpFromTraffic }.  The following week they were at { next.GP } which is { gpFromTraffic - next.GP } lower than it should be." );
					}

					if( doubleDecay ) {
						Console.WriteLine( $"\t!!! { Name } - Taking last week's GP value of { GP } and decaying it they should be at { decayedGP } if they did not receive any new loot.  The following week they were at { next.GP } which is { decayedGP - next.GP } lower than it should be.  The value of their decayed GP matches what it would have been if we decayed it twice." );
					}
				}
				if( _activeMode == Mode.Report || _activeMode == Mode.Both ) {
					Console.WriteLine( $"\t{ Name } - Missed Raid: { missedRaid } - Got Loot: { gotLoot } - Too Much EP: { tooMuchEP } (Expected { potentialNextEP } (Double Decay: {potentialNextEP2}), Got { next.EP }) - Too Little GP: { tooLittleGP } (Expected {decayedGP} (Double Decay: {decayedGP2}), Got {next.GP}) - Before: {EP}/{GP} - Decayed: {decayedEP}/{decayedGP} - After: {next.EP}/{next.GP}" );
				}
			}

		}

		private static void ShowLoot() {
			List<TrafficLogs.LogEntry> logs = _traffic.GetTrafficForPlayer( _playerFilter );
			foreach( var entry in logs ) {
				if( entry.GPBefore == entry.GPAfter ) {
					continue;
				}
				Console.WriteLine( $"{entry.ItemName} - { entry.Message } -- Before:{entry.GPBefore} - After:{entry.GPAfter} - {entry.Timestamp.ToLocalTime()}" );
			}
		}
	}
}
