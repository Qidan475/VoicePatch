using Exiled.API.Features;
using HarmonyLib;
using InventorySystem.Items.Coin;
using MEC;
using Mirror;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VoiceChat.Codec;
using VoiceChat.Networking;
using Mirror.LiteNetLib4Mirror;
using System.Diagnostics;

namespace VoicePatch
{
    [HarmonyPatch(typeof(VoiceTransceiver))]
    internal class VoicePatch
    {
        private static HashSet<int> _processedPackets = new HashSet<int>();

        private static ConcurrentBag<OpusDecoder> _decodersPool = new ConcurrentBag<OpusDecoder>();
        private static ConcurrentBag<byte[]> _rawDataPool = new ConcurrentBag<byte[]>();
        private static ConcurrentBag<float[]> _processedDataPool = new ConcurrentBag<float[]>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(VoiceTransceiver.ServerReceiveMessage))]
        private static bool ServerReceivedVcMessage(NetworkConnection conn, VoiceMessage msg)
        {
            if (_processedPackets.Remove((conn, msg).GetHashCode()))
                return true;

            if (msg.Speaker == null || conn.identity.netId != msg.Speaker.netId)
                return false;

            ProcessPacket(conn, msg);
            return false;
        }

        private static async Task ProcessPacket(NetworkConnection conn, VoiceMessage msg)
        {
            try
            {
                if (!_processedDataPool.TryTake(out var floats))
                    floats = new float[480];
                if (!_rawDataPool.TryTake(out var rawData))
                    rawData = new byte[512];

                Array.Copy(msg.Data, rawData, msg.Data.Length);
                var msgCopy = msg with { Data = rawData };
                var msgHash = (conn, msgCopy).GetHashCode();

                Timing.CallDelayed(EntryPoint.Instance.Config.VoicePacketDelayMilliseconds, () =>
                {
                    var isPassed = false;
                    lock (_processedPackets)
                    {
                        isPassed = _processedPackets.Contains(msgHash);
                    }

                    if (isPassed)
                        VoiceTransceiver.ServerReceiveMessage(conn, msgCopy);

                    _rawDataPool.Add(rawData);
                });

                var decodedLength = await Task.Run(() =>
                {
                    if (!_decodersPool.TryTake(out var decoder))
                        decoder = new OpusDecoder();

                    var decoded = decoder.Decode(rawData, rawData.Length, floats);
                    _decodersPool.Add(decoder);
                    return decoded;
                }).ConfigureAwait(false);

                if (decodedLength == 480)
                {
                    var lowest = 0f;
                    var highest = 0f;

                    foreach (var item in floats)
                    {
                        lowest = Mathf.Min(lowest, item);
                        highest = Mathf.Max(highest, item);
                    }

                    if (lowest > -5 && highest < 5)
                    {
                        lock (_processedPackets)
                        {
                            _processedPackets.Add(msgHash);
                        }
                    }
                }

                _processedDataPool.Add(floats);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
