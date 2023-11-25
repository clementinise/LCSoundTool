﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using JetBrains.Annotations;
using LCSoundTool.Patches;
using UnityEngine;
using UnityEngine.Networking;

namespace LCSoundTool
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class SoundTool : BaseUnityPlugin
    {
        private const string PLUGIN_GUID = "LCSoundTool";
        private const string PLUGIN_NAME = "LC Sound Tool";
        private const string PLUGIN_VERSION = "1.2.0";

        private readonly Harmony harmony = new Harmony(PLUGIN_GUID);

        public static SoundTool Instance;

        internal ManualLogSource logger;

        public KeyboardShortcut toggleAudioSourceDebugLog;
        public KeyboardShortcut toggleIndepthDebugLog;
        public bool wasKeyDown;
        public bool wasKeyDown2;

        public static bool debugAudioSources;
        public static bool indepthDebugging;

        private GameObject soundToolGameObject;
        public SoundToolUpdater updater { get; private set; }

        public static Dictionary<string, AudioClip> replacedClips { get; private set; }

        public static void ReplaceAudioClip(string originalName, AudioClip newClip)
        {
            if (string.IsNullOrEmpty(originalName))
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to replace an audio clip without original clip specified! This is not allowed.");
                return;
            }
            if (newClip == null)
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to replace an audio clip without new clip specified! This is not allowed.");
                return;
            }

            if (replacedClips.ContainsKey(originalName))
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to replace an audio clip that already has been replaced! This is not allowed.");
                return;
            }

            replacedClips.Add(originalName, newClip);
        }

        public static void ReplaceAudioClip(AudioClip originalClip, AudioClip newClip)
        {
            if (originalClip == null)
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to replace an audio clip without original clip specified! This is not allowed.");
                return;
            }
            if (newClip == null)
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to replace an audio clip without new clip specified! This is not allowed.");
                return;
            }

            ReplaceAudioClip(originalClip.name, newClip);
        }

        public static void RestoreAudioClip(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to restore an audio clip without original clip specified! This is not allowed.");
                return;
            }

            if (!replacedClips.ContainsKey(name))
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to restore an audio clip that does not exist! This is not allowed.");
                return;
            }

            replacedClips.Remove(name);
        }

        public static void RestoreAudioClip(AudioClip clip)
        {
            if (clip == null)
            {
                Instance.logger.LogWarning($"Plugin {PLUGIN_GUID} is trying to restore an audio clip without original clip specified! This is not allowed.");
                return;
            }

            RestoreAudioClip(clip.name);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                logger = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_GUID);

                logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");

                toggleAudioSourceDebugLog = new KeyboardShortcut(KeyCode.F5, new KeyCode[0]);
                toggleIndepthDebugLog = new KeyboardShortcut(KeyCode.F5, new KeyCode[1] { KeyCode.LeftAlt });

                debugAudioSources = false;
                indepthDebugging = false;

                replacedClips = new Dictionary<string, AudioClip>();

                harmony.PatchAll(typeof(AudioSourcePatch));
                harmony.PatchAll(typeof(NetworkSceneManagerPatch));

                SoundToolUpdater();
            }
        }

        private void Update()
        {
            SoundToolUpdater();
        }

        private void OnDestroy()
        {
            SoundToolUpdater();
        }

        private void SoundToolUpdater()
        {
            if (soundToolGameObject == null)
            {
                GameObject previous = GameObject.Find("SoundToolUpdater");

                if (previous != null)
                {
                    UnityEngine.Object.Destroy(previous);
                }

                soundToolGameObject = new GameObject("SoundToolUpdater");
                updater = soundToolGameObject.AddComponent<SoundToolUpdater>();
                updater.originSoundTool = this;

                if (Instance != null && Instance != this)
                {
                    UnityEngine.Object.Destroy(Instance);
                    Instance = this;
                }
                else if (Instance == null)
                {
                    Instance = this;
                }
            }
        }

        public static AudioClip GetAudioClip(string folder, string soundName)
        {
            bool tryLoading = true;

            // path stuff
            var path = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", folder, soundName);
            var pathDir = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", folder);
            // check if file and directory are valid, else skip loading
            if (!Directory.Exists(pathDir))
            {
                Instance.logger.LogWarning($"Requested directory does not exist! Creating new one at path: {pathDir}");
                Directory.CreateDirectory(pathDir);
                tryLoading = false;
            }
            if (!File.Exists(path))
            {
                Instance.logger.LogWarning($"Requested audio file does not exist at path: {path}!");
                tryLoading = false;
            }

            AudioClip result = null;

            if (tryLoading)
            {
                Instance.logger.LogDebug($"Loading AudioClip {soundName} from path: {path}");
                result = LoadClip(path);
                Instance.logger.LogDebug($"Finished loading AudioClip {soundName} with length of {result.length}!");
            }

            // return the clip we got
            return result;
        }

        static AudioClip LoadClip(string path)
        {
            AudioClip clip = null;
            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.WAV))
            {
                uwr.SendWebRequest();

                // we have to wrap tasks in try/catch, otherwise it will just fail silently
                try
                {
                    while (!uwr.isDone)
                    {
                        
                    }

                    if (uwr.result != UnityWebRequest.Result.Success)
                        Instance.logger.LogError($"Failed to load AudioClip from path: {path} Full error: {uwr.error}");
                    else
                    {
                        clip = DownloadHandlerAudioClip.GetContent(uwr);
                    }
                }
                catch (Exception err)
                {
                    Instance.logger.LogError($"{err.Message}, {err.StackTrace}");
                }
            }

            return clip;
        }
    }
}