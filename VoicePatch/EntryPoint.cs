using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoicePatch
{
    public class EntryPoint : Plugin<PluginConfig>
    {
        public override string Name => "VoicePatch";

        public static EntryPoint Instance { get; private set; }

        private Harmony _harmony;

        public override void OnEnabled()
        {
            Instance = this;
            _harmony = new Harmony("VoicePatch");
            _harmony.PatchAll();
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Instance = null;
            _harmony.UnpatchAll(_harmony.Id);
            base.OnDisabled();
        }
    }

    public class PluginConfig : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = true;

        [Description("Time to analyze each voice packet. Try to increase if voice chat becomes laggy, or decrease if you want smaller delay")]
        public int VoicePacketDelayMilliseconds { get; set; } = 200;
    }
}
