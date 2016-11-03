﻿using System;
using System.Linq;
using System.Text;

namespace PKHeX
{
    public sealed class SAV7 : SaveFile
    {
        // Save Data Attributes
        public override string BAKName => $"{FileName} [{OT} ({Version}) - {LastSavedTime}].bak";
        public override string Filter => "Main SAV|*.*";
        public override string Extension => "";
        public SAV7(byte[] data = null)
        {
            Data = data == null ? new byte[SaveUtil.SIZE_G7SMDEMO] : (byte[])data.Clone();
            BAK = (byte[])Data.Clone();
            Exportable = !Data.SequenceEqual(new byte[Data.Length]);

            // Load Info
            getBlockInfo();
            getSAVOffsets();

            HeldItems = Legal.HeldItems_SM;
            Personal = PersonalTable.SM;
            if (!Exportable)
                resetBoxes();
        }

        // Configuration
        public override SaveFile Clone() { return new SAV7(Data); }
        
        public override int SIZE_STORED => PKX.SIZE_6STORED;
        public override int SIZE_PARTY => PKX.SIZE_6PARTY;
        public override PKM BlankPKM => new PK7();
        public override Type PKMType => typeof(PK7);

        public override int BoxCount => 32;
        public override int MaxEV => 252;
        public override int Generation => 7;
        protected override int GiftCountMax => 48;
        protected override int GiftFlagMax => 0x100 * 8;
        protected override int EventFlagMax => -1;
        protected override int EventConstMax => (EventFlag - EventConst) / 2;
        public override int OTLength => 12;
        public override int NickLength => 12;

        public override int MaxMoveID => 720;
        public override int MaxSpeciesID => Legal.MaxSpeciesID_7;
        public override int MaxItemID => 920;
        public override int MaxAbilityID => 232;
        public override int MaxBallID => 0x1A; // 26
        public override int MaxGameID => 31; // MN

        // Feature Overrides
        public override bool HasGeolocation => true;

        // Blocks & Offsets
        private int BlockInfoOffset;
        private BlockInfo[] Blocks;
        private void getBlockInfo()
        {
            BlockInfoOffset = Data.Length - 0x200 + 0x10;
            if (BitConverter.ToUInt32(Data, BlockInfoOffset) != SaveUtil.BEEF)
                BlockInfoOffset -= 0x200; // No savegames have more than 0x3D blocks, maybe in the future?
            int count = (Data.Length - BlockInfoOffset - 0x8) / 8;
            BlockInfoOffset += 4;

            Blocks = new BlockInfo[count];
            int CurrentPosition = 0;
            for (int i = 0; i < Blocks.Length; i++)
            {
                Blocks[i] = new BlockInfo
                {
                    Offset = CurrentPosition,
                    Length = BitConverter.ToInt32(Data, BlockInfoOffset + 0 + 8 * i),
                    ID = BitConverter.ToUInt16(Data, BlockInfoOffset + 4 + 8 * i),
                    Checksum = BitConverter.ToUInt16(Data, BlockInfoOffset + 6 + 8 * i)
                };

                // Expand out to nearest 0x200
                CurrentPosition += Blocks[i].Length % 0x200 == 0 ? Blocks[i].Length : 0x200 - Blocks[i].Length % 0x200 + Blocks[i].Length;

                if ((Blocks[i].ID != 0) || i == 0) continue;
                count = i;
                break;
            }
            // Fix Final Array Lengths
            Array.Resize(ref Blocks, count);
        }
        protected override void setChecksums()
        {
            // Check for invalid block lengths
            if (Blocks.Length < 3) // arbitrary...
            {
                Console.WriteLine("Not enough blocks ({0}), aborting setChecksums", Blocks.Length);
                return;
            }
            // Apply checksums
            for (int i = 0; i < Blocks.Length; i++)
            {
                byte[] array = new byte[Blocks[i].Length];
                Array.Copy(Data, Blocks[i].Offset, array, 0, array.Length);
                BitConverter.GetBytes(SaveUtil.check16(array, Blocks[i].ID)).CopyTo(Data, BlockInfoOffset + 6 + i * 8);
            }

            // MemeCrypto -- provided dll is present.
            try
            {
                byte[] mcSAV = SaveUtil.Resign7(Data);
                if (mcSAV == new byte[0])
                    throw new Exception("MemeCrypto is not present. Dll may not be public at this time.");
                if (mcSAV == null)
                    throw new Exception("MemeCrypto received an invalid input.");
                Data = mcSAV;
            }
            catch (Exception e)
            {
                Util.Alert(e.Message, "Checksums have been applied but MemeCrypto has not.");
            }
        }
        public override bool ChecksumsValid
        {
            get
            {
                for (int i = 0; i < Blocks.Length; i++)
                {
                    byte[] array = new byte[Blocks[i].Length];
                    Array.Copy(Data, Blocks[i].Offset, array, 0, array.Length);
                    if (SaveUtil.check16(array, Blocks[i].ID) != BitConverter.ToUInt16(Data, BlockInfoOffset + 6 + i * 8))
                        return false;
                }
                return true;
            }
        }
        public override string ChecksumInfo
        {
            get
            {
                int invalid = 0;
                string rv = "";
                for (int i = 0; i < Blocks.Length; i++)
                {
                    byte[] array = new byte[Blocks[i].Length];
                    Array.Copy(Data, Blocks[i].Offset, array, 0, array.Length);
                    if (SaveUtil.check16(array, Blocks[i].ID) == BitConverter.ToUInt16(Data, BlockInfoOffset + 6 + i * 8))
                        continue;

                    invalid++;
                    rv += $"Invalid: {i.ToString("X2")} @ Region {Blocks[i].Offset.ToString("X5") + Environment.NewLine}";
                }
                // Return Outputs
                rv += $"SAV: {Blocks.Length - invalid}/{Blocks.Length + Environment.NewLine}";
                return rv;
            }
        }
        public override ulong? Secure1
        {
            get { return BitConverter.ToUInt64(Data, BlockInfoOffset - 0x14); }
            set { BitConverter.GetBytes(value ?? 0).CopyTo(Data, BlockInfoOffset - 0x14); }
        }
        public override ulong? Secure2
        {
            get { return BitConverter.ToUInt64(Data, BlockInfoOffset - 0xC); }
            set { BitConverter.GetBytes(value ?? 0).CopyTo(Data, BlockInfoOffset - 0xC); }
        }

        private void getSAVOffsets()
        {
            if (SMDEMO)
            {
                /* 00 */ Item           = 0x00000;  // [DE0]    MyItem
                /* 01 */ Trainer1       = 0x00E00;  // [07C]    Situation
                /* 02 */            //  = 0x01000;  // [014]    RandomGroup
                /* 03 */ TrainerCard    = 0x01200;  // [0C0]    MyStatus
                /* 04 */ Party          = 0x01400;  // [61C]    PokePartySave
                /* 05 */ EventFlag      = 0x01C00;  // [E00]    EventWork
                /* 06 */ PokeDex        = 0x02A00;  // [F78]    ZukanData
                /* 07 */ GTS            = 0x03A00;  // [228]    GtsData
                /* 08 */ Fused          = 0x03E00;  // [104]    UnionPokemon 
                /* 09 */ Misc           = 0x04000;  // [200]    Misc
                /* 10 */ Trainer2       = 0x04200;  // [020]    FieldMenu
                /* 11 */            //  = 0x04400;  // [004]    ConfigSave
                /* 12 */ AdventureInfo  = 0x04600;  // [058]    GameTime
                /* 13 */ PCLayout       = 0x04800;  // [5E6]    BOX
                /* 14 */ Box            = 0x04E00;  // [36600]  BoxPokemon
                /* 15 */ Resort         = 0x3B400;  // [572C]   ResortSave
                /* 16 */ PlayTime       = 0x40C00;  // [008]    PlayTime
                /* 17 */ //Overworld//  = 0x40E00;  // [1080]   FieldMoveModelSave
                /* 18 */            //  = 0x42000;  // [1A08]   Fashion
                /* 19 */            //  = 0x43C00;  // [6408]   JoinFestaPersonalSave
                /* 20 */            //  = 0x4A200;  // [6408]   JoinFestaPersonalSave
                /* 21 */            //  = 0x50800;  // [3998]   JoinFestaDataSave
                /* 22 */            //  = 0x54200;  // [100]    BerrySpot
                /* 23 */            //  = 0x54400;  // [100]    FishingSpot
                /* 24 */            //  = 0x54600;  // [10528]  LiveMatchData
                /* 25 */            //  = 0x64C00;  // [204]    BattleSpotData
                /* 26 */            //  = 0x65000;  // [B60]    PokeFinderSave
                /* 27 */ WondercardFlags = 0x65C00; // [3F50]   MysteryGiftSave
                /* 28 */            //  = 0x69C00;  // [358]    Record
                /* 29 */            //  = 0x6A000;  // [728]    Data Block
                /* 30 */            //  = 0x6A800;  // [200]    GameSyncSave
                /* 31 */            //  = 0x6AA00;  // [718]    PokeDiarySave
                /* 32 */            //  = 0x6B200;  // [1FC]    BattleInstSave
                /* 33 */ Daycare        = 0x6B400;  // [200]    Sodateya
                /* 34 */            //  = 0x6B600;  // [120]    WeatherSave
                /* 35 */            //  = 0x6B800;  // [1C8]    QRReaderSaveData
                /* 36 */            //  = 0x6BA00;  // [200]    TurtleSalmonSave

                OFS_PouchHeldItem =     Item + 0; // 430 (Case 0)
                OFS_PouchKeyItem =      Item + 0x6B8; // 184 (Case 4)
                OFS_PouchTMHM =         Item + 0x998; // 108 (Case 2)
                OFS_PouchMedicine =     Item + 0xB48; // 64 (Case 1)
                OFS_PouchBerry =        Item + 0xC48; // 72 (Case 3)
                OFS_PouchZCrystals =    Item + 0xD68; // 30 (Case 5)

                PokeDexLanguageFlags =  PokeDex + 0x550;
                WondercardData = WondercardFlags + 0x100;

                PCBackgrounds =         PCLayout + 0x5C0;
                LastViewedBox =         PCLayout + 0x5E5; // guess!?
            }
            else // Empty input
            {
                Party = 0x0;
                Box = Party + SIZE_PARTY * 6 + 0x1000;
            }
        }

        // Private Only
        private int Item { get; set; } = int.MinValue;
        private int AdventureInfo { get; set; } = int.MinValue;
        private int Trainer2 { get; set; } = int.MinValue;
        private int Misc { get; set; } = int.MinValue;
        private int LastViewedBox { get; set; } = int.MinValue;
        private int WondercardFlags { get; set; } = int.MinValue;
        private int PlayTime { get; set; } = int.MinValue;
        private int JPEG { get; set; } = int.MinValue;
        private int ItemInfo { get; set; } = int.MinValue;
        private int LinkInfo { get; set; } = int.MinValue;

        // Accessible as SAV7
        public int TrainerCard { get; private set; } = 0x14000;
        public int Resort { get; set; }
        public int PCFlags { get; private set; } = int.MinValue;
        public int PSSStats { get; private set; } = int.MinValue;
        public int MaisonStats { get; private set; } = int.MinValue;
        public int PCBackgrounds { get; private set; } = int.MinValue;
        public int Contest { get; private set; } = int.MinValue;
        public int Accessories { get; private set; } = int.MinValue;
        public int PokeDexLanguageFlags { get; private set; } = int.MinValue;

        private const int ResortCount = 93;
        public PKM[] ResortPKM
        {
            get
            {
                PKM[] data = new PKM[ResortCount];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = getPKM(getData(Resort + 0x12 + i * SIZE_STORED, SIZE_STORED));
                    data[i].Identifier = $"Resort Slot {i}";
                }
                return data;
            }
            set
            {
                if (value?.Length != ResortCount)
                    throw new ArgumentException();

                for (int i = 0; i < value.Length; i++)
                    setStoredSlot(value[i], Resort + 0x12 + i*SIZE_STORED);
            }
        }

        public override GameVersion Version
        {
            get
            {
                switch (Game)
                {
                    case 30: return GameVersion.SN;
                    case 31: return GameVersion.MN;
                }
                return GameVersion.Unknown;
            }
        }
        
        // Player Information
        public override ushort TID
        {
            get { return BitConverter.ToUInt16(Data, TrainerCard + 0); }
            set { BitConverter.GetBytes(value).CopyTo(Data, TrainerCard + 0); }
        }
        public override ushort SID
        {
            get { return BitConverter.ToUInt16(Data, TrainerCard + 2); }
            set { BitConverter.GetBytes(value).CopyTo(Data, TrainerCard + 2); }
        }
        public override int Game
        {
            get { return Data[TrainerCard + 4]; }
            set { Data[TrainerCard + 4] = (byte)value; }
        }
        public override int Gender
        {
            get { return Data[TrainerCard + 5]; }
            set { Data[TrainerCard + 5] = (byte)value; }
        }
        public override ulong? GameSyncID
        {
            get { return BitConverter.ToUInt64(Data, TrainerCard + 0x18); }
            set { BitConverter.GetBytes(value ?? 0).CopyTo(Data, TrainerCard + 0x18); }
        }
        public override int SubRegion
        {
            get { return Data[TrainerCard + 0x2E]; }
            set { Data[TrainerCard + 0x2E] = (byte)value; }
        }
        public override int Country
        {
            get { return Data[TrainerCard + 0x2F]; }
            set { Data[TrainerCard + 0x2F] = (byte)value; }
        }
        public override int ConsoleRegion
        {
            get { return Data[TrainerCard + 0x34]; }
            set { Data[TrainerCard + 0x34] = (byte)value; }
        }
        public override int Language
        {
            get { return Data[TrainerCard + 0x35]; }
            set { Data[TrainerCard + 0x35] = (byte)value; }
        }
        public override string OT
        {
            get { return Util.TrimFromZero(Encoding.Unicode.GetString(Data, TrainerCard + 0x38, 0x1A)); }
            set { Encoding.Unicode.GetBytes(value.PadRight(13, '\0')).CopyTo(Data, TrainerCard + 0x38); }
        }
        public int M
        {
            get { return BitConverter.ToUInt16(Data, Trainer1 + 0x00); } // could be anywhere 0x0-0x7
            set { BitConverter.GetBytes((ushort)value).CopyTo(Data, Trainer1 + 0x00); }
        }
        public float X
        {
            get { return BitConverter.ToSingle(Data, Trainer1 + 0x08); }
            set { BitConverter.GetBytes(value).CopyTo(Data, Trainer1 + 0x08); }
        }
        // 0xC probably rotation
        public float Z
        {
            get { return BitConverter.ToSingle(Data, Trainer1 + 0x10); }
            set { BitConverter.GetBytes(value).CopyTo(Data, Trainer1 + 0x10); }
        }
        public float Y
        {
            get { return (int)BitConverter.ToSingle(Data, Trainer1 + 0x20); }
            set { BitConverter.GetBytes(value).CopyTo(Data, Trainer1 + 0x20); }
        }

        public override uint Money
        {
            get { return BitConverter.ToUInt32(Data, Misc + 0x4); }
            set { BitConverter.GetBytes(value).CopyTo(Data, Misc + 0x4); }
        }
        public override int PlayedHours
        { 
            get { return BitConverter.ToUInt16(Data, PlayTime); } 
            set { BitConverter.GetBytes((ushort)value).CopyTo(Data, PlayTime); } 
        }
        public override int PlayedMinutes
        {
            get { return Data[PlayTime + 2]; }
            set { Data[PlayTime + 2] = (byte)value; } 
        }
        public override int PlayedSeconds
        {
            get { return Data[PlayTime + 3]; }
            set { Data[PlayTime + 3] = (byte)value; }
        }
        public uint LastSaved { get { return BitConverter.ToUInt32(Data, PlayTime + 0x4); } set { BitConverter.GetBytes(value).CopyTo(Data, PlayTime + 0x4); } }
        public int LastSavedYear { get { return (int)(LastSaved & 0xFFF); } set { LastSaved = LastSaved & 0xFFFFF000 | (uint)value; } }
        public int LastSavedMonth { get { return (int)(LastSaved >> 12 & 0xF); } set { LastSaved = LastSaved & 0xFFFF0FFF | ((uint)value & 0xF) << 12; } }
        public int LastSavedDay { get { return (int)(LastSaved >> 16 & 0x1F); } set { LastSaved = LastSaved & 0xFFE0FFFF | ((uint)value & 0x1F) << 16; } }
        public int LastSavedHour { get { return (int)(LastSaved >> 21 & 0x1F); } set { LastSaved = LastSaved & 0xFC1FFFFF | ((uint)value & 0x1F) << 21; } }
        public int LastSavedMinute { get { return (int)(LastSaved >> 26 & 0x3F); } set { LastSaved = LastSaved & 0x03FFFFFF | ((uint)value & 0x3F) << 26; } }
        public string LastSavedTime => $"{LastSavedYear.ToString("0000")}{LastSavedMonth.ToString("00")}{LastSavedDay.ToString("00")}{LastSavedHour.ToString("00")}{LastSavedMinute.ToString("00")}";

        public int ResumeYear { get { return BitConverter.ToInt32(Data, AdventureInfo + 0x4); } set { BitConverter.GetBytes(value).CopyTo(Data,AdventureInfo + 0x4); } }
        public int ResumeMonth { get { return Data[AdventureInfo + 0x8]; } set { Data[AdventureInfo + 0x8] = (byte)value; } }
        public int ResumeDay { get { return Data[AdventureInfo + 0x9]; } set { Data[AdventureInfo + 0x9] = (byte)value; } }
        public int ResumeHour { get { return Data[AdventureInfo + 0xB]; } set { Data[AdventureInfo + 0xB] = (byte)value; } }
        public int ResumeMinute { get { return Data[AdventureInfo + 0xC]; } set { Data[AdventureInfo + 0xC] = (byte)value; } }
        public int ResumeSeconds { get { return Data[AdventureInfo + 0xD]; } set { Data[AdventureInfo + 0xD] = (byte)value; } }
        public override int SecondsToStart { get { return BitConverter.ToInt32(Data, AdventureInfo + 0x28); } set { BitConverter.GetBytes(value).CopyTo(Data, AdventureInfo + 0x28); } }
        public override int SecondsToFame { get { return BitConverter.ToInt32(Data, AdventureInfo + 0x30); } set { BitConverter.GetBytes(value).CopyTo(Data, AdventureInfo + 0x30); } }
        
        // Inventory
        public override InventoryPouch[] Inventory
        {
            get
            {
                InventoryPouch[] pouch =
                {
                    new InventoryPouch(InventoryType.Medicine, Legal.Pouch_Medicine_SM, 999, OFS_PouchMedicine),
                    new InventoryPouch(InventoryType.Items, Legal.Pouch_Items_SM, 999, OFS_PouchHeldItem),
                    new InventoryPouch(InventoryType.TMHMs, Legal.Pouch_TMHM_SM, 1, OFS_PouchTMHM),
                    new InventoryPouch(InventoryType.Berries, Legal.Pouch_Berries_SM, 999, OFS_PouchBerry),
                    new InventoryPouch(InventoryType.KeyItems, Legal.Pouch_Key_SM, 1, OFS_PouchKeyItem),
                    new InventoryPouch(InventoryType.ZCrystals, Legal.Pouch_ZCrystal_SM, 999, OFS_PouchZCrystals),
                };
                foreach (var p in pouch)
                    p.getPouch7(ref Data);
                return pouch;
            }
            set
            {
                foreach (var p in value)
                    p.setPouch7(ref Data);
            }
        }

        // Storage
        public override int CurrentBox { get { return Data[LastViewedBox]; } set { Data[LastViewedBox] = (byte)value; } }
        public override int getPartyOffset(int slot)
        {
            return Party + SIZE_PARTY * slot;
        }
        public override int getBoxOffset(int box)
        {
            return Box + SIZE_STORED*box*30;
        }
        protected override int getBoxWallpaperOffset(int box)
        {
            int ofs = PCBackgrounds > 0 && PCBackgrounds < Data.Length ? PCBackgrounds : -1;
            if (ofs > -1)
                return ofs + box;
            return ofs;
        }
        public override void setBoxWallpaper(int box, int value)
        {
            if (PCBackgrounds < 0)
                return;
            int ofs = PCBackgrounds > 0 && PCBackgrounds < Data.Length ? PCBackgrounds : 0;
            Data[ofs + box] = (byte)value;
        }
        public override string getBoxName(int box)
        {
            if (PCLayout < 0)
                return "B" + (box + 1);
            return Util.TrimFromZero(Encoding.Unicode.GetString(Data, PCLayout + 0x22*box, 0x22));
        }
        public override void setBoxName(int box, string val)
        {
            Encoding.Unicode.GetBytes(val.PadRight(0x11, '\0')).CopyTo(Data, PCLayout + 0x22*box);
            Edited = true;
        }
        public override PKM getPKM(byte[] data)
        {
            return new PK7(data);
        }
        protected override void setPKM(PKM pkm)
        {
            PK7 pk7 = pkm as PK7;
            // Apply to this Save File
            int CT = pk7.CurrentHandler;
            DateTime Date = DateTime.Now;
            pk7.Trade(OT, TID, SID, Country, SubRegion, Gender, false, Date.Day, Date.Month, Date.Year);
            if (CT != pk7.CurrentHandler) // Logic updated Friendship
            {
                // Copy over the Friendship Value only under certain circumstances
                if (pk7.Moves.Contains(216)) // Return
                    pk7.CurrentFriendship = pk7.OppositeFriendship;
                else if (pk7.Moves.Contains(218)) // Frustration
                    pkm.CurrentFriendship = pk7.OppositeFriendship;
                else if (pk7.CurrentHandler == 1) // OT->HT, needs new Friendship/Affection
                    pk7.TradeFriendshipAffection(OT);
            }
            pkm.RefreshChecksum();
        }
        protected override void setDex(PKM pkm)
        {
            if (PokeDex < 0)
                return;
            if (pkm.Species == 0)
                return;
            if (pkm.Species > MaxSpeciesID) // Raw Max is 832
                return;
            if (Version == GameVersion.Unknown)
                return;

            const int brSize = 0x68; // 832 bits, 32bit alignment is required (MaxSpeciesID > 800)
            int baseOfs = PokeDex + 0x08;
            int bit = pkm.Species - 1;
            int bd = bit >> 3; // div8
            int bm = bit & 7; // mod8
            int gender = pkm.Gender % 2; // genderless -> male
            int shiny = pkm.IsShiny ? 1 : 0;
            int shift = gender + shiny << 2;
            if (pkm.Species == 327) // Spinda
            {
                if ((Data[PokeDex + 0x84] & (1 << (shift + 4))) != 0) // Already 2
                {
                    BitConverter.GetBytes(pkm.EncryptionConstant).CopyTo(Data, PokeDex + 0x8E8 + shift * 4);
                    // Data[PokeDex + 0x84] |= (byte)(1 << (shift + 4)); // 2 -- pointless
                    Data[PokeDex + 0x84] |= (byte)(1 << shift); // 1
                }
                else if ((Data[PokeDex + 0x84] & (1 << shift)) == 0) // Not yet 1
                {
                    Data[PokeDex + 0x84] |= (byte)(1 << shift); // 1
                }
            }
            int ofs = PokeDex // Raw Offset
                      + 0x08 // Magic + Flags
                      + 0x80; // Misc Data (1024 bits)

            // Set the Owned Flag
            Data[ofs + bd] |= (byte)(1 << bm);

            // Set the [Species/Gender/Shiny] Seen Flag
            int brSeen = (shift + 1) * brSize; // offset by 1 for the "Owned" Region
            Data[ofs + brSeen + bd] |= (byte)(1 << bm);

            // Set the Display flag if none are set
            bool Displayed = false;
            for (int i = 0; i < 4; i++)
            {
                int brDisplayed = (5 + i) * brSize; // offset by 1 for the "Owned" Region, 4 for the Seen Regions
                Displayed |= (Data[ofs + brDisplayed + bd] & (byte)(1 << bm)) != 0;
            }
            if (!Displayed)
                Data[ofs + brSeen + brSize * 4 + bd] |= (byte)(1 << bm); // Adjust brSeen to the displayed flags.

            // Set the Language
            int lang = pkm.Language;
            const int langCount = 9;
            if (lang <= 10 && lang != 6 && lang != 0) // valid language
            {
                if (lang >= 7)
                    lang--;
                lang--; // 0-8 languages
                if (lang < 0) lang = 1;
                int lbit = bit * langCount + lang;
                if (lbit >> 3 < 920) // Sanity check for max length of region
                    Data[PokeDexLanguageFlags + (lbit >> 3)] |= (byte)(1 << (lbit & 7));
            }
            return;

            // Set Form flags : TODO
#pragma warning disable 162
            int fc = Personal[pkm.Species].FormeCount;
            int f = SaveUtil.getDexFormIndexSM(pkm.Species, fc);
            if (f < 0) return;

            int FormLen = ORAS ? 0x26 : 0x18;
            int FormDex = PokeDex + 0x8 + brSize*9;
            int fbit = f + pkm.AltForm;
            int fbd = fbit>>3;
            int fbm = fbit&7;

            // Set Form Seen Flag
            Data[FormDex + FormLen*shiny + bit/8] |= (byte)(1 << (bit%8));

            // Set Displayed Flag if necessary, check all flags
            for (int i = 0; i < fc; i++)
            {
                int dfbit = f + i;
                int dfbd = dfbit>>3;
                int dfbm = dfbit&7;
                if ((Data[FormDex + FormLen*2 + dfbd] & (byte) (1 << dfbm)) != 0) // Nonshiny
                    return; // already set
                if ((Data[FormDex + FormLen*3 + dfbd] & (byte) (1 << dfbm)) != 0) // Shiny
                    return; // already set
            }
            Data[FormDex + FormLen * (2 + shiny) + fbd] |= (byte)(1 << fbm);
#pragma warning restore 162
        }
        public override byte[] decryptPKM(byte[] data)
        {
            return PKX.decryptArray(data);
        }
        public override int PartyCount
        {
            get { return Data[Party + 6 * SIZE_PARTY]; }
            protected set { Data[Party + 6 * SIZE_PARTY] = (byte)value; }
        }
        public override int BoxesUnlocked { get { return -1; } set { Data[PCFlags + 1] = (byte)(value + 1); } }
        public override byte[] BoxFlags {
            get { return null; }
            set
            {
                if (value.Length != 2) return;
                Data[PCFlags] = value[0];
                Data[PCFlags + 2] = value[1];
            }
        }

        public override int DaycareSeedSize => 32; // 128 bits
        public override int getDaycareSlotOffset(int loc, int slot)
        {
            if (loc != 0)
                return -1;
            if (Daycare < 0)
                return -1;
            return Daycare + 6 + slot * (SIZE_STORED + 6);
        }
        public override uint? getDaycareEXP(int loc, int slot)
        {
            if (loc != 0)
                return null;
            if (Daycare < 0)
                return null;

            return BitConverter.ToUInt32(Data, Daycare + (SIZE_STORED + 6) * slot + 2);
        }
        public override bool? getDaycareOccupied(int loc, int slot)
        {
            if (loc != 0)
                return null;
            if (Daycare < 0)
                return null;

            return Data[Daycare + (SIZE_STORED + 6) * slot] != 0;
        }
        public override string getDaycareRNGSeed(int loc)
        {
            if (loc != 0)
                return null;
            if (Daycare < 0)
                return null;

            var data = Data.Skip(Daycare + 0x1DC).Take(DaycareSeedSize / 2).Reverse().ToArray();
            return BitConverter.ToString(data).Replace("-", "");
        }
        public override bool? getDaycareHasEgg(int loc)
        {
            if (loc != 0)
                return null;
            if (Daycare < 0)
                return null;

            return Data[Daycare + 0x1E0] == 1;
        }
        public override void setDaycareEXP(int loc, int slot, uint EXP)
        {
            if (loc != 0)
                return;
            if (Daycare < 0)
                return;

            BitConverter.GetBytes(EXP).CopyTo(Data, Daycare + (SIZE_STORED + 6) * slot + 2);
        }
        public override void setDaycareOccupied(int loc, int slot, bool occupied)
        {
            if (loc != 0)
                return;
            if (Daycare < 0)
                return;

            // Are they using species instead of a flag?
            Data[Daycare + (SIZE_STORED + 6) * slot] = (byte)(occupied ? 1 : 0);
        }
        public override void setDaycareRNGSeed(int loc, string seed)
        {
            if (loc != 0)
                return;
            if (Daycare < 0)
                return;
            if (seed == null)
                return;
            if (seed.Length > DaycareSeedSize)
                return;

            Enumerable.Range(0, seed.Length)
                 .Where(x => x % 2 == 0)
                 .Reverse()
                 .Select(x => Convert.ToByte(seed.Substring(x, 2), 16))
                 .Reverse().ToArray().CopyTo(Data, Daycare + 0x1DC);
        }
        public override void setDaycareHasEgg(int loc, bool hasEgg)
        {
            if (loc != 0)
                return;
            if (Daycare < 0)
                return;

            Data[Daycare + 0x1E0] = (byte)(hasEgg ? 1 : 0);
        }

        // Mystery Gift
        protected override bool[] MysteryGiftReceivedFlags
        {
            get
            {
                if (WondercardData < 0 || WondercardFlags < 0)
                    return null;

                bool[] r = new bool[(WondercardData-WondercardFlags)*8];
                for (int i = 0; i < r.Length; i++)
                    r[i] = (Data[WondercardFlags + (i>>3)] >> (i&7) & 0x1) == 1;
                return r;
            }
            set
            {
                if (WondercardData < 0 || WondercardFlags < 0)
                    return;
                if ((WondercardData - WondercardFlags)*8 != value?.Length)
                    return;

                byte[] data = new byte[value.Length/8];
                for (int i = 0; i < value.Length; i++)
                    if (value[i])
                        data[i>>3] |= (byte)(1 << (i&7));

                data.CopyTo(Data, WondercardFlags);
                Edited = true;
            }
        }
        protected override MysteryGift[] MysteryGiftCards
        {
            get
            {
                if (WondercardData < 0)
                    return null;
                MysteryGift[] cards = new MysteryGift[GiftCountMax];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = getWC6(i);

                return cards;
            }
            set
            {
                if (value == null)
                    return;
                if (value.Length > GiftCountMax)
                    Array.Resize(ref value, GiftCountMax);
                
                for (int i = 0; i < value.Length; i++)
                    setWC6(value[i], i);
                for (int i = value.Length; i < GiftCountMax; i++)
                    setWC6(new WC6(), i);
            }
        }

        public byte[] LinkBlock
        {
            get
            {
                if (LinkInfo < 0)
                    return null;
                return Data.Skip(LinkInfo).Take(0xC48).ToArray();
            }
            set
            {
                if (LinkInfo < 0)
                    return;
                if (value.Length != 0xC48)
                    return;
                value.CopyTo(Data, LinkInfo);
            }
        }

        private WC6 getWC6(int index)
        {
            if (WondercardData < 0)
                return null;
            if (index < 0 || index > GiftCountMax)
                return null;

            return new WC6(Data.Skip(WondercardData + index * WC6.Size).Take(WC6.Size).ToArray());
        }
        private void setWC6(MysteryGift wc6, int index)
        {
            if (WondercardData < 0)
                return;
            if (index < 0 || index > GiftCountMax)
                return;

            wc6.Data.CopyTo(Data, WondercardData + index * WC6.Size);

            for (int i = 0; i < GiftCountMax; i++)
                if (BitConverter.ToUInt16(Data, WondercardData + i * WC6.Size) == 0)
                    for (int j = i + 1; j < GiftCountMax - i; j++) // Shift everything down
                        Array.Copy(Data, WondercardData + j * WC6.Size, Data, WondercardData + (j - 1) * WC6.Size, WC6.Size);

            Edited = true;
        }

        // Writeback Validity
        public override string MiscSaveChecks()
        {
            string r = "";
            byte[] FFFF = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
            for (int i = 0; i < Data.Length / 0x200; i++)
            {
                if (!FFFF.SequenceEqual(Data.Skip(i * 0x200).Take(0x200))) continue;
                r = $"0x200 chunk @ 0x{(i*0x200).ToString("X5")} is FF'd."
                    + Environment.NewLine + "Cyber will screw up (as of August 31st 2014)." + Environment.NewLine + Environment.NewLine;

                // Check to see if it is in the Pokedex
                if (i * 0x200 > PokeDex && i * 0x200 < PokeDex + 0x900)
                {
                    r += "Problem lies in the Pokedex. ";
                    if (i * 0x200 == PokeDex + 0x400)
                        r += "Remove a language flag for a species < 585, ie Petilil";
                }
                break;
            }
            return r;
        }
        public override string MiscSaveInfo()
        {
            return Blocks.Aggregate("", (current, b) => current +
                $"{b.ID.ToString("00")}: {b.Offset.ToString("X5")}-{(b.Offset + b.Length).ToString("X5")}, {b.Length.ToString("X5")}{Environment.NewLine}");
        }
    }
}
