﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHeX
{
    public partial class LegalityAnalysis
    {
        private PKM pkm;
        private readonly List<CheckResult> Parse = new List<CheckResult>();

        private object EncounterMatch;
        private Type EncounterType;
        private List<MysteryGift> EventGiftMatch;
        private CheckResult Encounter, History;
        private int[] RelearnBase;
        // private bool SecondaryChecked;

        public readonly bool Parsed;
        public readonly bool Valid;
        public CheckResult[] vMoves = new CheckResult[4];
        public CheckResult[] vRelearn = new CheckResult[4];
        public string Report => getLegalityReport();
        public string VerboseReport => getVerboseLegalityReport();
        public bool Native => pkm.GenNumber == pkm.Format;

        public LegalityAnalysis(PKM pk)
        {
            try
            {
                switch (pk.Format)
                {
                    case 6: parsePK6(pk); break;
                    case 7: parsePK7(pk); break;
                    default: return;
                }
                Valid = Parse.Any() && Parse.All(chk => chk.Valid);
                if (vMoves.Any(m => m.Valid != true))
                    Valid = false;
                else if (vRelearn.Any(m => m.Valid != true))
                    Valid = false;
            }
            catch { Valid = false; }
            Parsed = true;

            if (pkm.FatefulEncounter && vRelearn.Any(chk => !chk.Valid) && EncounterMatch == null)
                AddLine(Severity.Indeterminate, "Fateful Encounter with no matching Encounter. Has the Mystery Gift data been contributed?", CheckIdentifier.Fateful);
        }

        private void AddLine(Severity s, string c, CheckIdentifier i)
        {
            AddLine(new CheckResult(s, c, i));
        }
        private void AddLine(CheckResult chk)
        {
            Parse.Add(chk);
        }
        private void parsePK6(PKM pk)
        {
            if (!(pk is PK6))
                return;
            pkm = pk;

            updateRelearnLegality();
            updateMoveLegality();
            updateChecks();
            getLegalityReport();
        }
        private void parsePK7(PKM pk)
        {
            if (!(pk is PK7))
                return;
            pkm = pk;

            updateRelearnLegality();
            updateMoveLegality();
            updateChecks();
            getLegalityReport();
        }

        private void updateRelearnLegality()
        {
            try { vRelearn = verifyRelearn(); }
            catch { for (int i = 0; i < 4; i++) vRelearn[i] = new CheckResult(Severity.Invalid, "Internal error.", CheckIdentifier.RelearnMove); }
            // SecondaryChecked = false;
        }
        private void updateMoveLegality()
        {
            try { vMoves = verifyMoves(); }
            catch { for (int i = 0; i < 4; i++) vMoves[i] = new CheckResult(Severity.Invalid, "Internal error.", CheckIdentifier.Move); }
            // SecondaryChecked = false;
        }

        private void updateChecks()
        {
            Encounter = verifyEncounter();
            EncounterType = EncounterMatch?.GetType().BaseType;
            History = verifyHistory();

            verifyECPID();
            verifyNickname();
            verifyID();
            verifyIVs();
            verifyEVs();
            verifyLevel();
            verifyRibbons();
            verifyAbility();
            verifyBall();
            verifyOTMemory();
            verifyHTMemory();
            verifyRegion();
            verifyForm();
            verifyMisc();
            verifyGender();
            // SecondaryChecked = true;
        }
        private string getLegalityReport()
        {
            if (!Parsed)
                return "Analysis not available for this Pokémon.";
            
            string r = "";
            for (int i = 0; i < 4; i++)
                if (!vMoves[i].Valid)
                    r += $"{vMoves[i].Judgement} Move {i + 1}: {vMoves[i].Comment}" + Environment.NewLine;
            for (int i = 0; i < 4; i++)
                if (!vRelearn[i].Valid)
                    r += $"{vRelearn[i].Judgement} Relearn Move {i + 1}: {vRelearn[i].Comment}" + Environment.NewLine;

            if (r.Length == 0 && Parse.All(chk => chk.Valid))
                return "Legal!";
            
            // Build result string...
            r += Parse.Where(chk => !chk.Valid).Aggregate("", (current, chk) => current + $"{chk.Judgement}: {chk.Comment}{Environment.NewLine}");

            return r.TrimEnd();
        }
        private string getVerboseLegalityReport()
        {
            string r = getLegalityReport() + Environment.NewLine;
            if (pkm == null)
                return r;
            r += "===" + Environment.NewLine + Environment.NewLine;
            int rl = r.Length;

            for (int i = 0; i < 4; i++)
                if (vMoves[i].Valid)
                    r += $"{vMoves[i].Judgement} Move {i + 1}: {vMoves[i].Comment}" + Environment.NewLine;
            for (int i = 0; i < 4; i++)
                if (vRelearn[i].Valid)
                    r += $"{vRelearn[i].Judgement} Relearn Move {i + 1}: {vRelearn[i].Comment}" + Environment.NewLine;

            if (rl != r.Length) // move info added, break for next section
                r += Environment.NewLine;
            
            r += Parse.Where(chk => chk != null && chk.Valid && chk.Comment != "Valid").OrderBy(chk => chk.Judgement) // Fishy sorted to top
                .Aggregate("", (current, chk) => current + $"{chk.Judgement}: {chk.Comment}{Environment.NewLine}");
            return r.TrimEnd();
        }

        public int[] getSuggestedRelearn()
        {
            if (RelearnBase == null)
                return new int[4];
            if (pkm.GenNumber < 6)
                return new int[4];

            if (!pkm.WasEgg)
                return RelearnBase;

            List<int> window = new List<int>(RelearnBase);

            for (int i = 0; i < 4; i++)
                if (!vMoves[i].Valid || vMoves[i].Flag)
                    window.Add(pkm.Moves[i]);

            if (window.Count < 4)
                window.AddRange(new int[4 - window.Count]);
            return window.Skip(window.Count - 4).Take(4).ToArray();
        }
    }
}
