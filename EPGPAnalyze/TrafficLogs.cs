using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EPGPAnalyze
{
	public class TrafficLogs
	{
		public Dictionary<string, List<LogEntry>> _entries = new Dictionary<string, List<LogEntry>>();

		public TrafficLogs() {
		}

		public async Task ParseLogs( string path ) {
			bool trafficFound = false;
			using( StreamReader sr = new StreamReader( path ) ) {
				string line = await sr.ReadLineAsync();
				while( line != null ) {
					if( !trafficFound ) {
						if( line.Contains( "TRAFFIC = {" ) ) {
							trafficFound = true;
						}
						else {
							line = await sr.ReadLineAsync();
						}

						continue;
					}


					LogEntry entry = new LogEntry();
					bool keepGoing = await entry.ReadData( sr );
					if( !keepGoing ) {
						break;
					}

					List<LogEntry> existing = new List<LogEntry>();
					if( _entries.ContainsKey( entry.Target ) ) {
						existing = _entries[ entry.Target ];
					}
					else {
						_entries[ entry.Target ] = existing;
					}

					existing.Add( entry );
				}
			}
		}

		public List<LogEntry> GetTrafficForPlayer( string playerName ) {
			if( !_entries.ContainsKey( playerName ) ) {
				return new List<LogEntry>();
			}

			return _entries[ playerName ];
		}

		private static Regex StringValRegex = new Regex( "^\\s*\"(.*)\",", RegexOptions.Compiled );
		private static Regex ItemValRegex = new Regex( "^\\s*\".*Hitem:([0-9]+).*?\\[(.*?)\\].*", RegexOptions.Compiled );
		private static Regex TimestampValRegex = new Regex( ".*?([0-9][0-9]+)", RegexOptions.Compiled );

		private const bool DEBUG = false;

		public class LogEntry {
			public string   Target    { get; set; }
			public string   Giver     { get; set; }
			public string   Message   { get; set; }
			public int      EPBefore  { get; set; }
			public int      EPAfter   { get; set; }
			public int      GPBefore  { get; set; }
			public int      GPAfter   { get; set; }
			public int      ItemID    { get; set; }
			public string   ItemName  { get; set; }
			public DateTime Timestamp { get; set; }

			public LogEntry() {
			}

			private async Task<string> ReadLine( StreamReader sr ) {
				string line = await sr.ReadLineAsync();
				if( DEBUG ) {
					Console.WriteLine( line );
				}

				return line;
			}

			public async Task<bool> ReadData( StreamReader sr ) {
				string firstLine = await ReadLine( sr ); //Opening Bracket
				if( firstLine.Contains( "}" ) ) {
					return false;
				}

				Target  = StringValRegex.Match( await ReadLine( sr ) ).Groups[1].Value;
				Giver   = StringValRegex.Match( await ReadLine( sr ) ).Groups[1].Value;
				Message = StringValRegex.Match( await ReadLine( sr ) ).Groups[1].Value;
				EPBefore = await GetInt( sr );
				EPAfter = await GetInt( sr );
				GPBefore = await GetInt( sr );
				GPAfter = await GetInt( sr );

				string itemLine = await ReadLine( sr );
				string timestampLine = itemLine;
				Match itemMatch = ItemValRegex.Match( itemLine );
				if( itemMatch.Success ) {
					ItemID = int.Parse( itemMatch.Groups[1].Value );
					ItemName = itemMatch.Groups[2].Value;
					timestampLine = await ReadLine( sr );
				}

				bool TryTimestamp() {
					Match timestampMatch = TimestampValRegex.Match( timestampLine );
					try {
						int secondsSince = int.Parse( timestampMatch.Groups[1].Value );
						Timestamp = DateTimeOffset.FromUnixTimeSeconds( secondsSince ).DateTime;
						return true;
					}
					catch( Exception ) {
						return false;
					}
				}

				if( TryTimestamp() == false ) {
					timestampLine = await ReadLine( sr );
					if( TryTimestamp() == false ) {
						throw new Exception( "Failed to get timestamp" );
					}
				}


				while( true ) {
					string bracket = await ReadLine( sr ); //Closing Bracket
					if( bracket.Trim().StartsWith( "}" ) ) {
						break;
					}
				}

				//Console.WriteLine( $"{ Target } - { Message } - { GPBefore } - { GPAfter } - { ItemName }" );

				return true;
			}

			private async Task<int> GetInt( StreamReader sr ) {
				string line = await ReadLine( sr );
				try {
					Match m = StringValRegex.Match( line );
					if( string.IsNullOrWhiteSpace( m.Groups[1].Value ) ) {
						return -1;
					}

					return int.Parse( m.Groups[1].Value );
				}
				catch( Exception ) {
					Console.WriteLine( "Offending Line: " + line );
					throw;
				}
			}
		}
	}
}
