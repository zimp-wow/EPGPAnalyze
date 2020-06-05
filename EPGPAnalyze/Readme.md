This tool takes EPGP snapshots produced by the CEPGP addon in WoW and compares them with the assumption that a decay has happened between the two.

Between that assumption as well as a few others we can detect instances of potential tampering that warrant additional investigation.

To run the tool you need to copy your snapshots into the same directory as the tool, they need to use the following naming convention:

CCEPGPYYYYMMDD.txt

where YYYY = the year (2020)
        MM = the month (06)
        DD = the day   (04)

The contents of the file should be the data copied out of the CEPGP addon when you click the export standings button.  These standings should be exported BEFORE each decay is performed.

You will also need to get your 'CEPGP.lua' file found in the your WOW install directory, for example: D:\Battle.Net\World of Warcraft\_classic_\WTF\Account\ACCOUNTNAME\SavedVariables\CEPGP.lua

This file should also be placed in the same directory as the tool.  It contains the EPGP traffic on who received items which is important for detecting additional sources of tampering.  You should seek to get this file
from someone with high/perfect raid attendance since the traffic logs do not work if you are not online.

Once you have all the files you can run the tool.  By default it is in 'Analyze' mode which simply reports potential violations on every person found in the snapshots.  It also supports the following additional modes of operation:

./EPGPAnalyze.exe Report

Instead of showing you a violation it simply shows you a report of each player's points and item awards for each week.

./EPGPAnalyze.exe Both

Shows you the output from both the Analyze and Report modes.

Also, you can add a second argument to specify a player name to generate output only for that player.  So when you want to further investigate a potential violation its best to just do:

./EPGPAnalyze.exe Both Playername

*Note* Player names with special characters cause issues, my tools tries to strip out those special characters.  So for example 'Ausl$nder' becomes 'Auslnder'.  The playername argument accepts partial matches so you could type:

./EPGPAnalyze.exe Both Ausl

to see a report for 'Ausl$nder' (and anyone else with a name that starts with Ausl).

For reading the report please use this guide:

PlayerName - Missed Raid: True - Got Loot: False - Too Much EP: False (Expected 939 (Double Decay: 874), Got 644) - Too Little GP: False (Expected 103 (Double Decay: 92), Got 103) - Before: 716/109 - Decayed: 644/103 - After: 644/103
	How to read the line above:
             Missed raid: We take last week's EP decay it and add the max possible EP award to it, if the value in this report is less than that then I deduced that you missed some of the raids after last week's decay.
             Got loot: We take last week's GP and decay it.  If this week's GP is higher than the decayed value then they must have gotten loot.
             Too much EP: We take last week's EP decay it and add the max possible EP award to it then check to see if the value in this report was higher than that which indicates they got extra EP somehow.
             Too little GP: We take last week's GP and decay it then compare it to this week's GP value.  If this week's GP is lower than expected then they somehow lost GP.
             Before Values: The EP/GP values from the previous week's report (pre-decay)
             Decayed Values: The EP/GP values from the previous week's report after I decay them
             After Values: The EP/GP values from this report.


Please use this tool responsibly.  The results need to be carefully analyzed and interpreted before pointing fingers.  It is very hard to catch every edge case and identify it which results in a lot of false positives.
