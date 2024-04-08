using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using IMFM = ImmersiveFootsteps.ImmersiveFootstepsMain;

namespace ImmersiveFootsteps
{
    public class ImmersiveFootstepsObject : MonoBehaviour
    {
        public static ImmersiveFootstepsObject Instance;

        #region Fields

        public float swimInterval = 1.75f;
        public Vector3 lastPosition;
        public float distance = 0f;

        public float footstepTimer = 0f;
        public float plateSwayTimer = 0f;
        public float chainSwayTimer = 0f;
        public float leatherSwayTimer = 0f;
        public int refreshSlotsTimer = 0;
        public bool isWalking = false;
        public bool altStep = false;

        public float volumeScale = 1f;

        GameObject playerAdvanced;
        DaggerfallAudioSource dfAudioSource;
        PlayerMotor playerMotor;
        PlayerEnterExit playerEnterExit;
        TransportManager transportManager;

        AudioClip[] currentClimateFootsteps = IMFM.PathFootstepsMain;

        DaggerfallDateTime.Seasons lastSeason = DaggerfallDateTime.Seasons.Summer;
        int lastClimateIndex = (int)MapsFile.Climates.Ocean;
        public int lastTileMapIndex = 0;

        DaggerfallDateTime.Seasons currentSeason = DaggerfallDateTime.Seasons.Summer;
        int currentClimateIndex = (int)MapsFile.Climates.Ocean;
        int currentTileMapIndex = 0;

        bool isInside = false;

        #endregion

        #region Properties

        public AudioClip[] CurrentClimateFootsteps
        {
            get { return currentClimateFootsteps; }
            set { currentClimateFootsteps = value; }
        }

        #endregion

        private void Start()
        {
            Instance = this;

            playerAdvanced = GameManager.Instance.PlayerObject;
            dfAudioSource = playerAdvanced.GetComponent<DaggerfallAudioSource>();
            playerMotor = GetComponent<PlayerMotor>();
            playerEnterExit = GetComponent<PlayerEnterExit>();
            transportManager = GameManager.Instance.TransportManager;

            // Set start position
            lastPosition = GetHorizontalPosition();
        }

        private void FixedUpdate()
        {
            if (GameManager.IsGamePaused || SaveLoadManager.Instance.LoadInProgress)
                return;

            if (IMFM.TravelOptionsCheck)
            {
                if (CheckForTravelOptionsAcceleratedTravel())
                    return;
            }

            bool playerSwimming = false;

            refreshSlotsTimer++;

            if (refreshSlotsTimer >= 250) // 50 FixedUpdates is approximately equal to 1 second since each FixedUpdate happens every 0.02 seconds, that's what Unity docs say at least.
            {
                refreshSlotsTimer = 0;
                if (IMFM.AllowFootstepSounds || IMFM.AllowArmorSwaySounds) { IMFM.RefreshEquipmentSlotReferences(); }
                else { } // Do nothing if both armor sound options are disabled, since there is no point in updating these values in that case.
            }

            if (playerMotor == null)
            {
                footstepTimer = 0f;
                plateSwayTimer = 0f;
                chainSwayTimer = 0f;
                leatherSwayTimer = 0f;
                return;
            }

            if (playerEnterExit != null)
            {
                playerSwimming = playerEnterExit.IsPlayerSwimming;
            }

            if (playerMotor.IsGrounded == false && !playerSwimming)
            {
                return;
            }

            if (playerMotor.IsStandingStill)
            {
                return;
            }

            if (playerMotor.IsRunning)
            {
                footstepTimer += 1.5f * Time.fixedDeltaTime;
                plateSwayTimer += 1.8f * Time.fixedDeltaTime;
                chainSwayTimer += 1.8f * Time.fixedDeltaTime;
                leatherSwayTimer += 1.8f * Time.fixedDeltaTime;
                volumeScale = 1.25f;
            }
            else if (playerMotor.IsMovingLessThanHalfSpeed)
            {
                footstepTimer += 0.7f * Time.fixedDeltaTime;
                plateSwayTimer += 0.5f * Time.fixedDeltaTime;
                chainSwayTimer += 0.5f * Time.fixedDeltaTime;
                leatherSwayTimer += 0.5f * Time.fixedDeltaTime;
                volumeScale = 0.6f;
            }
            else
            {
                footstepTimer += Time.fixedDeltaTime;
                plateSwayTimer += Time.fixedDeltaTime;
                chainSwayTimer += Time.fixedDeltaTime;
                leatherSwayTimer += Time.fixedDeltaTime;
                volumeScale = 1f;
            }

            if (transportManager.TransportMode == TransportModes.Horse || transportManager.TransportMode == TransportModes.Cart)
            {
                footstepTimer = 0f;
            }

            if (!IMFM.AllowFootstepSounds)
            {
                footstepTimer = 0f;
            }

            if (!IMFM.AllowArmorSwaySounds)
            {
                plateSwayTimer = 0f;
                chainSwayTimer = 0f;
                leatherSwayTimer = 0f;
            }    

            // Honestly, for now I think I'm just going to keep the bug where when on the player ship the footstep sounds are not always accurate on the player ship exterior.
            // The reason being right now I don't feel like having the "TransportManager.IsOnShip" method running every frame, or close to it is worth potentially fixing that.
            // Maybe I'll try to fix it later if I get many complaints or whatever, but for now I'll just leave it as is, in-case peformance could be impacted.
            // Hopefully I'll find a better way to resolve this later, but for right now just leave it, oh well.
            if (playerSwimming)
            {
                // Get distance player travelled horizontally
                Vector3 position = GetHorizontalPosition();
                distance += Vector3.Distance(position, lastPosition);
                lastPosition = position;

                if (distance > swimInterval)
                {
                    isInside = (playerEnterExit == null) ? true : playerEnterExit.IsPlayerInside;

                    if (isInside)
                    {
                        DetermineInteriorClimateFootstep();
                    }
                    else
                    {
                        DetermineExteriorClimateFootstep();
                    }

                    dfAudioSource.AudioSource.PlayOneShot(RollRandomFootstepAudioClip(currentClimateFootsteps), volumeScale * IMFM.FootstepVolumeMulti * DaggerfallUnity.Settings.SoundVolume);

                    distance = 0f;
                }

                footstepTimer = 0f;
                plateSwayTimer = 0f;
                chainSwayTimer = 0f;
                leatherSwayTimer = 0f;
            }

            if (footstepTimer >= IMFM.stepInterval)
            {
                isInside = (playerEnterExit == null) ? true : playerEnterExit.IsPlayerInside;

                if (isInside)
                {
                    DetermineInteriorClimateFootstep();
                }
                else
                {
                    DetermineExteriorClimateFootstep();
                }

                if (IMFM.AllowFootstepSounds)
                {
                    dfAudioSource.AudioSource.PlayOneShot(RollRandomFootstepAudioClip(currentClimateFootsteps), volumeScale * IMFM.FootstepVolumeMulti * DaggerfallUnity.Settings.SoundVolume);
                }

                // Reset the footstepTimer
                footstepTimer = 0f;
            }

            if (IMFM.leatherWornSwayWeight <= 0) { leatherSwayTimer = 0f; }
            if (IMFM.chainWornSwayWeight <= 0) { chainSwayTimer = 0f; }
            if (IMFM.plateWornSwayWeight <= 0) { plateSwayTimer = 0f; }

            if (leatherSwayTimer >= IMFM.leatherSwayInterval)
            {
                if (IMFM.AllowArmorSwaySounds)
                    dfAudioSource.AudioSource.PlayOneShot(RollRandomArmorSwayAudioClip(IMFM.LeatherSwaying), volumeScale * IMFM.ArmorSwayVolumeMulti * DaggerfallUnity.Settings.SoundVolume);

                leatherSwayTimer = 0f;
                IMFM.leatherSwayInterval = UnityEngine.Random.Range(IMFM.ArmorSwayFrequency + 0.1f, IMFM.ArmorSwayFrequency + 0.4f) - (IMFM.leatherWornSwayWeight * 0.02f);
            }

            if (chainSwayTimer >= IMFM.chainSwayInterval)
            {
                if (IMFM.AllowArmorSwaySounds)
                    dfAudioSource.AudioSource.PlayOneShot(RollRandomArmorSwayAudioClip(IMFM.ChainmailSwaying), volumeScale * IMFM.ArmorSwayVolumeMulti * DaggerfallUnity.Settings.SoundVolume);

                chainSwayTimer = 0f;
                IMFM.leatherSwayInterval = UnityEngine.Random.Range(IMFM.ArmorSwayFrequency + 0.1f, IMFM.ArmorSwayFrequency + 0.4f) - (IMFM.leatherWornSwayWeight * 0.02f);
            }

            if (plateSwayTimer >= IMFM.plateSwayInterval)
            {
                if (IMFM.AllowArmorSwaySounds)
                    dfAudioSource.AudioSource.PlayOneShot(RollRandomArmorSwayAudioClip(IMFM.PlateSwaying), volumeScale * IMFM.ArmorSwayVolumeMulti * DaggerfallUnity.Settings.SoundVolume);

                plateSwayTimer = 0f;
                IMFM.leatherSwayInterval = UnityEngine.Random.Range(IMFM.ArmorSwayFrequency + 0.1f, IMFM.ArmorSwayFrequency + 0.4f) - (IMFM.leatherWornSwayWeight * 0.02f);
            }
        }

        public void DetermineInteriorClimateFootstep()
        {
            // Use water sounds if in dungeon water
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon && playerEnterExit.blockWaterLevel != 10000)
            {
                // In water, deep depth
                if (playerEnterExit.IsPlayerSwimming)
                {
                    currentClimateFootsteps = altStep ? IMFM.DeepWaterFootstepsAlt : IMFM.DeepWaterFootstepsMain;
                }
                // In water, shallow depth
                else if (!playerEnterExit.IsPlayerSwimming && (playerMotor.transform.position.y - 0.57f) < (playerEnterExit.blockWaterLevel * -1 * MeshReader.GlobalScale))
                {
                    currentClimateFootsteps = altStep ? IMFM.ShallowWaterFootstepsAlt : IMFM.ShallowWaterFootstepsMain;
                }
                else
                {
                    CheckToUseArmorFootsteps();
                }
            }
        }

        public void DetermineExteriorClimateFootstep()
        {
            currentSeason = DaggerfallUnity.Instance.WorldTime.Now.SeasonValue;
            currentClimateIndex = GameManager.Instance.PlayerGPS.CurrentClimateIndex;
            currentTileMapIndex = GameManager.Instance.StreamingWorld.PlayerTileMapIndex;

            if (lastTileMapIndex != currentTileMapIndex || lastClimateIndex != currentClimateIndex || lastSeason != currentSeason)
            {
                lastSeason = currentSeason;
                lastClimateIndex = currentClimateIndex;
                lastTileMapIndex = currentTileMapIndex;

                if (currentTileMapIndex == 0)
                {
                    // Minor bug here, if you are water-walking over water tiles, like the ocean, but water walking wears off, the "shallow water" sound will still play, due to not updating, oh well for now.
                    if (GameManager.Instance.PlayerMotor.OnExteriorWater == PlayerMotor.OnExteriorWaterMethod.WaterWalking) { currentClimateFootsteps = altStep ? IMFM.ShallowWaterFootstepsAlt : IMFM.ShallowWaterFootstepsMain; }
                    else { currentClimateFootsteps = altStep ? IMFM.DeepWaterFootstepsAlt : IMFM.DeepWaterFootstepsMain; }
                }
                else if (CheckClimateTileTables("Shallow_Water", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.ShallowWaterFootstepsAlt : IMFM.ShallowWaterFootstepsMain; }
                else if (CheckClimateTileTables("Path", (byte)currentTileMapIndex))
                {
                    CheckToUseArmorFootsteps();
                }
                else if (currentSeason == DaggerfallDateTime.Seasons.Winter && IsSnowyClimate(currentClimateIndex))
                {
                    if (currentClimateIndex == (int)MapsFile.Climates.Swamp && CheckClimateTileTables("Swamp_Snow_Alt", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.MudFootstepsAlt : IMFM.MudFootstepsMain; }
                    else { currentClimateFootsteps = altStep ? IMFM.SnowFootstepsAlt : IMFM.SnowFootstepsMain; }
                }
                else if (IsGrassyClimate(currentClimateIndex))
                {
                    if (CheckClimateTileTables("Temperate_Dirt", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.GravelFootstepsAlt : IMFM.GravelFootstepsMain; } // Gravel
                    else if (CheckClimateTileTables("Temperate_Stone", (byte)currentTileMapIndex)) { CheckToUseArmorFootsteps(); } // Stone
                    else { currentClimateFootsteps = altStep ? IMFM.GrassFootstepsAlt : IMFM.GrassFootstepsMain; } // Grass
                }
                else if (IsRockyClimate(currentClimateIndex))
                {
                    if (CheckClimateTileTables("Mountain_Dirt", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.GravelFootstepsAlt : IMFM.GravelFootstepsMain; } // Gravel
                    else if (CheckClimateTileTables("Mountain_Stone", (byte)currentTileMapIndex)) { CheckToUseArmorFootsteps(); } // Stone
                    else { currentClimateFootsteps = altStep ? IMFM.GrassFootstepsAlt : IMFM.GrassFootstepsMain; } // Grass
                }
                else if (IsSandyClimate(currentClimateIndex))
                {
                    if (CheckClimateTileTables("Desert_Gravel", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.GravelFootstepsAlt : IMFM.GravelFootstepsMain; } // Gravel
                    else if (CheckClimateTileTables("Desert_Stone", (byte)currentTileMapIndex)) { CheckToUseArmorFootsteps(); } // Stone
                    else { currentClimateFootsteps = altStep ? IMFM.SandFootstepsAlt : IMFM.SandFootstepsMain; } // Sand
                }
                else if (IsSwampyClimate(currentClimateIndex))
                {
                    if (CheckClimateTileTables("Swamp_Bog", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.MudFootstepsAlt : IMFM.MudFootstepsMain; } // Mud
                    else if (CheckClimateTileTables("Swamp_Grass", (byte)currentTileMapIndex)) { currentClimateFootsteps = altStep ? IMFM.GrassFootstepsAlt : IMFM.GrassFootstepsMain; } // Grass
                    else { currentClimateFootsteps = altStep ? IMFM.MudFootstepsAlt : IMFM.MudFootstepsMain; } // Mud
                }
            }

            if (currentClimateFootsteps.Length <= 0)
            {
                currentClimateFootsteps = altStep ? IMFM.PathFootstepsAlt : IMFM.PathFootstepsMain;
            }
        }

        #region Climate Type Checks

        public static bool IsSnowyClimate(int climateIndex)
        {
            // These are all the existing climates that DO NOT get snow on the ground during the winter season, in the vanilla game atleast.
            switch (climateIndex)
            {
                case (int)MapsFile.Climates.Desert:
                case (int)MapsFile.Climates.Desert2:
                case (int)MapsFile.Climates.Rainforest:
                case (int)MapsFile.Climates.Subtropical:
                    return false;
                default:
                    return true;
            }
        }

        public static bool IsGrassyClimate(int climateIndex)
        {
            switch (climateIndex)
            {
                case (int)MapsFile.Climates.Woodlands:
                case (int)MapsFile.Climates.HauntedWoodlands:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRockyClimate(int climateIndex)
        {
            switch (climateIndex)
            {
                case (int)MapsFile.Climates.Mountain:
                case (int)MapsFile.Climates.MountainWoods:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSandyClimate(int climateIndex)
        {
            switch (climateIndex)
            {
                case (int)MapsFile.Climates.Desert:
                case (int)MapsFile.Climates.Desert2:
                case (int)MapsFile.Climates.Subtropical:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSwampyClimate(int climateIndex)
        {
            switch (climateIndex)
            {
                case (int)MapsFile.Climates.Swamp:
                case (int)MapsFile.Climates.Rainforest:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Climate Tile Checks

        static Dictionary<byte, bool> shallowWaterTileLookup = new Dictionary<byte, bool>
        {
            {5,true}, {6,true}, {8,true}, {20,true}, {21,true}, {23,true}, {30,true}, {31,true}, {33,true}, {34,true}, {35,true}, {36,true}, {49,true}
        };

        static Dictionary<byte, bool> pathTileLookup = new Dictionary<byte, bool>
        {
            {46,true}, {47,true}, {55,true}
        };

        static Dictionary<byte, bool> temperateDirtTileLookup = new Dictionary<byte, bool>
        {
            {1,true}, {4,true}, {7,true}, {10,true}, {13,true}, {25,true}, {26,true}, {28,true}, {37,true}, {38,true}, {39,true}, {51,true}, {52,true}, {54,true}
        };

        static Dictionary<byte, bool> temperateStoneTileLookup = new Dictionary<byte, bool>
        {
            {3,true}, {14,true}, {16,true}, {17,true}, {24,true}, {27,true}, {29,true}, {32,true}, {43,true}, {44,true}, {45,true}, {50,true}
        };

        static Dictionary<byte, bool> mountainDirtTileLookup = new Dictionary<byte, bool>
        {
            {1,true}, {4,true}, {7,true}, {10,true}, {13,true}, {25,true}, {26,true}, {28,true}, {37,true}, {38,true}, {39,true}, {51,true}, {52,true}, {54,true}
        };

        static Dictionary<byte, bool> mountainStoneTileLookup = new Dictionary<byte, bool>
        {
            {3,true}, {14,true}, {16,true}, {17,true}, {24,true}, {27,true}, {29,true}, {32,true}, {43,true}, {44,true}, {45,true}, {50,true}
        };

        static Dictionary<byte, bool> desertGravelTileLookup = new Dictionary<byte, bool>
        {
            {2,true}, {9,true}, {11,true}, {12,true}, {15,true}, {18,true}, {19,true}, {22,true}, {39,true}, {40,true}, {41,true}, {42,true}, {45,true}, {53,true}
        };

        static Dictionary<byte, bool> desertStoneTileLookup = new Dictionary<byte, bool>
        {
            {3,true}, {14,true}, {16,true}, {17,true}, {24,true}, {26,true}, {27,true}, {29,true}, {32,true}, {38,true}, {43,true}, {44,true}
        };

        static Dictionary<byte, bool> swampBogTileLookup = new Dictionary<byte, bool>
        {
            {1,true}, {4,true}, {7,true}, {10,true}, {13,true}, {25,true}, {26,true}, {28,true}, {37,true}, {38,true}, {39,true}, {50,true}, {51,true}, {52,true}, {54,true}
        };

        static Dictionary<byte, bool> swampGrassTileLookup = new Dictionary<byte, bool>
        {
            {3,true}, {14,true}, {16,true}, {17,true}, {24,true}, {27,true}, {29,true}, {32,true}, {43,true}, {44,true}, {45,true}
        };

        static Dictionary<byte, bool> swampAlternateTileLookup = new Dictionary<byte, bool>
        {
            {1,true}, {7,true}, {10,true}, {11,true}, {13,true}, {25,true}, {26,true}, {28,true}, {37,true}, {38,true}, {39,true}, {43,true}, {48,true}, {50,true}, {52,true}
        };

        static Dictionary<string, Dictionary<byte, bool>> climateFloorTilesLookup = new Dictionary<string, Dictionary<byte, bool>>
        {
            {"Shallow_Water",shallowWaterTileLookup}, {"Path",pathTileLookup}, {"Temperate_Dirt",temperateDirtTileLookup}, {"Temperate_Stone",temperateStoneTileLookup},
            {"Mountain_Dirt",mountainDirtTileLookup}, {"Mountain_Stone",mountainStoneTileLookup}, {"Desert_Gravel",desertGravelTileLookup}, {"Desert_Stone",desertStoneTileLookup},
            {"Swamp_Bog",swampBogTileLookup}, {"Swamp_Grass",swampGrassTileLookup}, {"Swamp_Snow_Alt",swampAlternateTileLookup}
        };

        public static bool CheckClimateTileTables(string climateKey, byte tileKey)
        {
            if (climateFloorTilesLookup.ContainsKey(climateKey))
            {
                if (climateFloorTilesLookup[climateKey].ContainsKey(tileKey))
                {
                    return climateFloorTilesLookup[climateKey][tileKey];
                }
                else { return false; }
            }
            else { return false; }
        }

        #endregion

        #region Building Interior Climate Floor Checks

        static Dictionary<string, bool> buildingTileFloorArchiveLookup = new Dictionary<string, bool>
        {
            {"16_3",true}, {"37_3",true}, {"40_3",true}, {"41_0",true}, {"41_1",true}, {"41_2",true}, {"41_3",true}, {"44_3",true}, {"63_3",true}, {"111_2",true}, {"111_3",true},
            {"137_3",true}, {"141_0",true}, {"141_1",true}, {"141_2",true}, {"141_3",true}, {"144_3",true}, {"311_3",true}, {"337_3",true}, {"341_0",true}, {"341_1",true},
            {"341_2",true}, {"341_3",true}, {"363_3",true}, {"411_3",true}, {"437_3",true}, {"440_3",true}, {"444_3",true}, {"463_3",true}
        };

        static Dictionary<string, bool> buildingStoneFloorArchiveLookup = new Dictionary<string, bool>
        {
            {"60_3",true}, {"140_3",true}, {"160_3",true}, {"163_3",true}, {"340_3",true}, {"360_3",true}, {"366_3",true}, {"416_3",true}, {"460_3",true}, {"466_3",true}
        };

        static Dictionary<string, bool> buildingWoodFloorArchiveLookup = new Dictionary<string, bool>
        {
            {"28_3",true}, {"66_3",true}, {"116_3",true}, {"128_3",true}, {"166_3",true}, {"171_3",true}, {"316_3",true}, {"328_3",true}, {"344_3",true}, {"428_3",true}
        };

        static Dictionary<string, Dictionary<string, bool>> buildingClimateFloorTypesLookup = new Dictionary<string, Dictionary<string, bool>>
        {
            {"Tile_Floor",buildingTileFloorArchiveLookup}, {"Stone_Floor",buildingStoneFloorArchiveLookup}, {"Wood_Floor",buildingWoodFloorArchiveLookup}
        };

        public static bool CheckBuildingClimateFloorTypeTables(string floorTypeKey, string archiveKey)
        {
            if (buildingClimateFloorTypesLookup.ContainsKey(floorTypeKey))
            {
                if (buildingClimateFloorTypesLookup[floorTypeKey].ContainsKey(archiveKey))
                {
                    return buildingClimateFloorTypesLookup[floorTypeKey][archiveKey];
                }
                else { return false; }
            }
            else { return false; }
        }

        #endregion

        #region Acrobat Motor Message Listeners

        // Capture this message so we can play fall damage sound
        private void ApplyPlayerFallDamage(float fallDistance)
        {
            // Play falling damage one-shot through normal audio source
            if (dfAudioSource)
                dfAudioSource.AudioSource.PlayOneShot(CheckToUseHardFallSounds(true), 4f * IMFM.FootstepVolumeMulti * DaggerfallUnity.Settings.SoundVolume);
        }

        // Capture this message so we can play hard fall sound
        private void HardFallAlert(float fallDistance)
        {
            // Play hard fall one-shot through normal audio source
            if (dfAudioSource)
                dfAudioSource.AudioSource.PlayOneShot(CheckToUseHardFallSounds(false), 4f * IMFM.FootstepVolumeMulti * DaggerfallUnity.Settings.SoundVolume);
        }

        // Capture this message so we can play large splash sound
        public void PlayLargeSplash()
        {
            if (dfAudioSource)
                dfAudioSource.AudioSource.PlayOneShot(IMFM.WaterLandingSound[0], 4f * IMFM.FootstepVolumeMulti * DaggerfallUnity.Settings.SoundVolume);
        }

        #endregion

        public void CheckToUseArmorFootsteps()
        {
            DaggerfallUnityItem boots = IMFM.WornBoots;
            if (boots != null)
            {
                if (boots.NativeMaterialValue >= (int)ArmorMaterialTypes.Iron)
                {
                    currentClimateFootsteps = altStep ? IMFM.PlateFootstepsAlt : IMFM.PlateFootstepsMain;
                }
                else if (boots.NativeMaterialValue >= (int)ArmorMaterialTypes.Chain)
                {
                    currentClimateFootsteps = altStep ? IMFM.ChainmailFootstepsAlt : IMFM.ChainmailFootstepsMain;
                }
                else
                {
                    currentClimateFootsteps = altStep ? IMFM.LeatherFootstepsAlt : IMFM.LeatherFootstepsMain;
                }
            }
            else
            {
                currentClimateFootsteps = altStep ? IMFM.UnarmoredFootstepsAlt : IMFM.UnarmoredFootstepsMain;
            }
        }

        public AudioClip CheckToUseHardFallSounds(bool fallDamage)
        {
            DaggerfallUnityItem boots = IMFM.WornBoots;
            if (boots != null)
            {
                if (boots.NativeMaterialValue >= (int)ArmorMaterialTypes.Iron)
                {
                    return fallDamage ? IMFM.PlateHardLanding[1] : IMFM.PlateHardLanding[0];
                }
                else if (boots.NativeMaterialValue >= (int)ArmorMaterialTypes.Chain)
                {
                    return fallDamage ? IMFM.ChainmailHardLanding[1] : IMFM.ChainmailHardLanding[0];
                }
                else
                {
                    return fallDamage ? IMFM.LeatherHardLanding[1] : IMFM.LeatherHardLanding[0];
                }
            }
            else
            {
                return fallDamage ? IMFM.UnarmoredHardLanding[1] : IMFM.UnarmoredHardLanding[0];
            }
        }

        public static bool CheckForTravelOptionsAcceleratedTravel()
        {
            bool accelTravelActive = false;

            DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance.SendModMessage("TravelOptions", "isTravelActive", null, (string message, object data) =>
            {
                accelTravelActive = (bool)data;
            });

            return accelTravelActive;
        }

        private Vector3 GetHorizontalPosition()
        {
            return new Vector3(transform.position.x, transform.position.y, transform.position.z);
        }

        public static bool CoinFlip()
        {
            if (UnityEngine.Random.Range(0, 1 + 1) == 0)
                return false;
            else
                return true;
        }

        // Made these two different methods because I didn't feel like figuring out a "clean" way to use the same one tracking both "LastAudioClipPlayed" values, oh well for now.
        public static AudioClip RollRandomFootstepAudioClip(AudioClip[] clips)
        {
            int randChoice = UnityEngine.Random.Range(0, clips.Length);
            AudioClip clip = clips[randChoice];

            if (clip == IMFM.LastFootstepPlayed)
            {
                if (randChoice == 0)
                    randChoice++;
                else if (randChoice == clips.Length - 1)
                    randChoice--;
                else
                    randChoice = CoinFlip() ? randChoice + 1 : randChoice - 1;

                clip = clips[randChoice];
            }
            IMFM.LastFootstepPlayed = clip;
            return clip;
        }

        // Made these two different methods because I didn't feel like figuring out a "clean" way to use the same one tracking both "LastAudioClipPlayed" values, oh well for now.
        public static AudioClip RollRandomArmorSwayAudioClip(AudioClip[] clips)
        {
            int randChoice = UnityEngine.Random.Range(0, clips.Length);
            AudioClip clip = clips[randChoice];

            if (clip == IMFM.LastSwaySoundPlayed)
            {
                if (randChoice == 0)
                    randChoice++;
                else if (randChoice == clips.Length - 1)
                    randChoice--;
                else
                    randChoice = CoinFlip() ? randChoice + 1 : randChoice - 1;

                clip = clips[randChoice];
            }
            IMFM.LastSwaySoundPlayed = clip;
            return clip;
        }

        public static AudioClip GetTestFootstepClip()
        {
            AudioClip clip = null;

            clip = RollRandomFootstepAudioClip(IMFM.TestFootstepSound);

            if (clip == null)
                clip = IMFM.TestFootstepSound[0];

            return clip;
        }
    }
}