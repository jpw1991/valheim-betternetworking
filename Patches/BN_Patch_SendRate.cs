﻿using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_SendRate {
        private const int DEFAULT_SEND_BUFFER_SIZE = 524288; // 524288 is the Steam default and Valheim does not currently change it
        private const int SEND_BUFFER_SIZE = DEFAULT_SEND_BUFFER_SIZE * 10;

        public enum Options_NetworkSendRateMax {
            [Description("No limit <b>[default]</b>")]
            _INF,
            [Description("400% (600 KB/s | 4.8 Mbit/s)")]
            _400,
            [Description("300% (450 KB/s | 3.6 Mbit/s)")]
            _300,
            [Description("200% (300 KB/s | 2.4 Mbit/s)")]
            _200,
            [Description("150% (225 KB/s | 1.8 Mbit/s)")]
            _150,
            [Description("100% (150 KB/s | 1.2 Mbit/s)")]
            _100,
            [Description("50% (75 KB/s | 0.6 Mbit/s)")]
            _50
        }
        public enum Options_NetworkSendRateMin {
            [Description("400% (600 KB/s | 4.8 Mbit/s)")]
            _400,
            [Description("300% (450 KB/s | 3.6 Mbit/s) <b>[default]</b>")]
            _300,
            [Description("200% (300 KB/s | 2.4 Mbit/s)")]
            _200,
            [Description("150% (225 KB/s | 1.8 Mbit/s)")]
            _150,
            [Description("100% (150 KB/s | 1.2 Mbit/s)")]
            _100,
            [Description("50% (75 KB/s | 0.6 Mbit/s)")]
            _50
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkSendRateMin = config.Bind(
                "Networking",
                "Minimum Send Rate",
                Options_NetworkSendRateMin._300,
                new ConfigDescription(
                    "The minimum speed Steam can <i>attempt</i> to send data.\n" +
                    "<b>Lower this below your internet upload speed.</b>"
                ));
            BetterNetworking.configNetworkSendRateMax = config.Bind(
                "Networking",
                "Maximum Send Rate",
                Options_NetworkSendRateMax._INF,
                new ConfigDescription(
                    "The maximum speed Steam can <i>attempt</i> to send data.\n" +
                    "If you have a low upload speed, lower this <i>below</i> your internet upload speed."
                ));

            ConfigNetworkSendRateSettings_Listen();
        }

        public static void ConfigNetworkSendRateSettings_Listen() {
            BetterNetworking.configNetworkSendRateMin.SettingChanged += ConfigNetworkSendRateMin_SettingChanged;
            BetterNetworking.configNetworkSendRateMax.SettingChanged += ConfigNetworkSendRateMax_SettingChanged;
            BN_Logger.LogInfo("Started listening for user changes to NetworkSendRates");
        }

        private static void ConfigNetworkSendRateMin_SettingChanged(object sender, EventArgs e) {
            if ((int)BetterNetworking.configNetworkSendRateMin.Value+1 < (int)BetterNetworking.configNetworkSendRateMax.Value) {
                BetterNetworking.configNetworkSendRateMax.Value = (Options_NetworkSendRateMax)(BetterNetworking.configNetworkSendRateMin.Value+1);
                BN_Logger.LogInfo("Maximum network send rate automatically increased");
            }
            NetworkSendRate_Patch.SetSendRateMinFromConfig();
        }
        private static void ConfigNetworkSendRateMax_SettingChanged(object sender, EventArgs e) {
            if ((int)BetterNetworking.configNetworkSendRateMax.Value > (int)BetterNetworking.configNetworkSendRateMin.Value+1) {
                BetterNetworking.configNetworkSendRateMin.Value = (Options_NetworkSendRateMin)(BetterNetworking.configNetworkSendRateMax.Value-1);
                BN_Logger.LogInfo("Minimum network send rate automatically decreased");
            }
            NetworkSendRate_Patch.SetSendRateMaxFromConfig();
        }

        [HarmonyPatch(typeof(SteamNetworkingUtils))]
        [HarmonyPatch(typeof(SteamGameServerNetworkingUtils))]
        class NetworkSendRate_Patch {
            static private int originalNetworkSendRateMin = 0;
            static private bool originalNetworkSendRateMin_set = false;
            static private int originalNetworkSendRateMax = 0;
            static private bool originalNetworkSendRateMax_set = false;
            static private bool networkSendBufferSize_set = false;


            public static int SendRateMin {
                get {
                    switch (BetterNetworking.configNetworkSendRateMin.Value) {
                        case Options_NetworkSendRateMin._400:
                            return originalNetworkSendRateMin * 4;
                        case Options_NetworkSendRateMin._300:
                            return originalNetworkSendRateMin * 3;
                        case Options_NetworkSendRateMin._200:
                            return originalNetworkSendRateMin * 2;
                        case Options_NetworkSendRateMin._150:
                            return originalNetworkSendRateMin * 3/2;
                        case Options_NetworkSendRateMin._50:
                            return originalNetworkSendRateMin / 2;
                    }
                    return originalNetworkSendRateMin;
                }
            }
            public static int SendRateMax {
                get {
                    switch (BetterNetworking.configNetworkSendRateMax.Value) {
                        case Options_NetworkSendRateMax._INF:
                            return 0;
                        case Options_NetworkSendRateMax._400:
                            return originalNetworkSendRateMax * 4;
                        case Options_NetworkSendRateMax._300:
                            return originalNetworkSendRateMax * 3;
                        case Options_NetworkSendRateMax._200:
                            return originalNetworkSendRateMax * 2;
                        case Options_NetworkSendRateMax._150:
                            return originalNetworkSendRateMax * 3/2;
                        case Options_NetworkSendRateMax._50:
                            return originalNetworkSendRateMax / 2;
                    }
                    return originalNetworkSendRateMax;
                }
            }

            public static void SetSendRateMinFromConfig() {
                if (!originalNetworkSendRateMin_set) {
                    BN_Logger.LogInfo("Attempted to set NetworkSendRateMin before Valheim did");
                    return;
                }

                BN_Logger.LogMessage($"Setting NetworkSendRateMin to {SendRateMin}");
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SendRateMin);
            }
            public static void SetSendRateMaxFromConfig() {
                if (!originalNetworkSendRateMax_set) {
                    BN_Logger.LogInfo("Attempted to set NetworkSendRateMax before Valheim did");
                    return;
                }

                BN_Logger.LogMessage($"Setting NetworkSendRateMax to {SendRateMax}");
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SendRateMax);
            }
            public static void SetSendBufferSize() {
                networkSendBufferSize_set = true; // if the buffer sized is changed outside of this method, Valheim set it
                BN_Logger.LogMessage($"Setting SendBufferSize to {SEND_BUFFER_SIZE} (dedicated:{BN_Utils.IsDedicated()})");
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SEND_BUFFER_SIZE);
            }

            private static void SetSteamNetworkConfig(ESteamNetworkingConfigValue valueType, int value) {
                if (ZNet.instance == null) {
                    BN_Logger.LogInfo("Attempted to set Steam networking config value while disconnected");
                    return;
                }

                GCHandle pinned_SendRate = GCHandle.Alloc(value, GCHandleType.Pinned);

                try {
                    if (BN_Utils.IsDedicated()) {
                        SteamGameServerNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    } else {
                        SteamNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    }
                } catch {
                    BN_Logger.LogError("Unable to set networking config; please notify the mod author");
                }

                pinned_SendRate.Free();
            }

            [HarmonyPatch(nameof(SteamNetworkingUtils.SetConfigValue))]
            [HarmonyPatch(nameof(SteamGameServerNetworkingUtils.SetConfigValue))]
            static void Prefix(
                ESteamNetworkingConfigValue eValue,
                ESteamNetworkingConfigScope eScopeType,
                IntPtr scopeObj,
                ESteamNetworkingConfigDataType eDataType,
                ref IntPtr pArg) {

                if (eScopeType == ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global &&
                    scopeObj == IntPtr.Zero &&
                    eDataType == ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32) {

                    switch (eValue) {
                        case ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin:
                            if (!originalNetworkSendRateMin_set) {
                                originalNetworkSendRateMin_set = true;
                                originalNetworkSendRateMin = Marshal.ReadInt32(pArg);

                                BN_Logger.LogMessage($"Valheim's default NetworkSendRateMin is {originalNetworkSendRateMin}");
                            }
                            break;
                        case ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax:
                            if (!originalNetworkSendRateMax_set) {
                                originalNetworkSendRateMax_set = true;
                                originalNetworkSendRateMax = Marshal.ReadInt32(pArg);

                                BN_Logger.LogMessage($"Valheim's default NetworkSendRateMax is {originalNetworkSendRateMin}");
                            }
                            break;
                        case ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize:
                            if (!networkSendBufferSize_set) {
                                BN_Logger.LogWarning("Valheim set the SendBufferSize unexpectedly");
                            }
                            break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket), "RegisterGlobalCallbacks")]
        class PreventValheimControlOfNetworkRate_Patch {

            static void Postfix() {
                BN_Logger.LogInfo("Network settings overwritten by Valheim");

                NetworkSendRate_Patch.SetSendRateMinFromConfig();
                NetworkSendRate_Patch.SetSendRateMaxFromConfig();
                NetworkSendRate_Patch.SetSendBufferSize();
            }
        }
    }
}