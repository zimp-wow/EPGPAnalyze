using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EPGPAnalyze
{
    class Program
    {
        const int MOLTEN_CORE_EP = 126;
        const int BWL_ONY_EP     = 169;

        private static Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        static async Task Main(string[] args)
        {
            SortedSet<string> sorted = new SortedSet<string>( new SortFiles() );

            string[] files = Directory.GetFiles( ".", "*CCEPGP*" );
            foreach( string file in files ) {
                sorted.Add( file );
            }

            foreach( string file in sorted ) {
                await ProcessFile( file );
            }

            Console.ReadLine();
        }

        private static async Task ProcessFile( string file ) {
            Console.WriteLine( "\nProcessing File: " + file + "\n" );
            using( StreamReader sr = new StreamReader( file ) ) {
                while( !sr.EndOfStream ) {
                    string line  = await sr.ReadLineAsync();

                    try {
                        Entry entry = new Entry( line );
                        if( _entries.ContainsKey( entry.Name ) ) {
                            Entry existing = _entries[ entry.Name ];

                            existing.Analyze( entry );
                        }

                        _entries[ entry.Name ] = entry;
                    }
                    catch( Exception e ) {
                        Console.WriteLine( "Failed to parse line: " + line );
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
            public string Name  { get; set; }
            public string Class { get; set; }
            public string Role  { get; set; }
            public int    EP    { get; set; }
            public int    GP    { get; set; }
            public double PR    { get; set; }

            public Entry( string line ) {
                string[] comps = line.Split( ',' );
                if( line.Length < 6 ) {
                    throw new Exception( "Unexpected number of fields" );
                }

                Name  = comps[0];
                Class = comps[1];
                Role  = comps[2];
                EP    = int.Parse( comps[3] );
                GP    = int.Parse( comps[4] );
                PR    = double.Parse( comps[5] );
            }

            public void Analyze( Entry next ) {
                int decayGP( int value, float percent = 0.1f, bool baseGP = true ) {
                    if( baseGP ) {
                        return value - (int)Math.Ceiling((value - 50) * percent);
                    }
                    return value - (int)Math.Ceiling(value * percent);
                }

                int decayedEP = EP - (int)Math.Ceiling(EP * .1f);
                int decayedGP = decayGP( GP );
                int decayedGP2 = decayGP( GP, .1f, false );

                int potentialNextEP = decayedEP + BWL_ONY_EP + MOLTEN_CORE_EP;
                bool missedRaid = false;
                bool tooMuchEP = false;
                if( next.EP < potentialNextEP ) {
                    if( potentialNextEP - next.EP > 1 ) {
                        missedRaid = true;
                    }
                }
                if( next.EP > potentialNextEP ) {
                    if( next.EP - potentialNextEP > 1 ) {
                        tooMuchEP = true;
                    }
                }

                bool gotLoot = false;
                bool tooLittleGP = false;
                bool doubleDecay = false;
                if( next.GP > decayedGP ) {
                    if( next.GP - decayedGP > 1 ) {
                        gotLoot = true;
                    }
                }
                if( next.GP < decayedGP ) {
                    if( decayedGP - next.GP > 1 ) {
                        tooLittleGP = true;
                    }
                }
                if( next.GP == decayedGP2 && next.GP != 50 ) {
                    doubleDecay = true;
                }

                if( tooMuchEP || tooLittleGP || doubleDecay ) {
                    Console.WriteLine( $"{ Name } - Missed Raid: { missedRaid } - Got Loot: { gotLoot } - Too Much EP: { tooMuchEP } (Expected { potentialNextEP }, Got { next.EP }) - Too Little GP: { tooLittleGP } (Expected {decayedGP} (Double Decay: {decayedGP2}), Got {next.GP}) - Before: {EP}/{GP} - Decayed: {decayedEP}/{decayedGP} - After: {next.EP}/{next.GP}" );
                }
            }
        }
    }
}
