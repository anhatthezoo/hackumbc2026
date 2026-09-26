using System;
using System.Collections.Generic;
using RoyaltyBoat.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    internal sealed class VisualSettingsUIBinder : IDisposable
    {
        private const string VolumePreference = "settings.master-volume";

        private readonly DropdownField resolutionDropdown;
        private readonly DropdownField antiAliasingDropdown;
        private readonly Slider volumeSlider;
        private readonly Slider cameraShakeSlider;
        private readonly Toggle fullscreenToggle;
        private readonly Toggle vsyncToggle;

        public VisualSettingsUIBinder(VisualElement root)
        {
            resolutionDropdown = root.Q<DropdownField>("resolution-dropdown");
            antiAliasingDropdown = root.Q<DropdownField>("anti-aliasing-dropdown");
            volumeSlider = root.Q<Slider>("volume-slider");
            cameraShakeSlider = root.Q<Slider>("camera-shake-slider");
            fullscreenToggle = root.Q<Toggle>("fullscreen-toggle");
            vsyncToggle = root.Q<Toggle>("vsync-toggle");

            resolutionDropdown.RegisterValueChangedCallback(HandleResolutionChanged);
            antiAliasingDropdown.RegisterValueChangedCallback(HandleAntiAliasingChanged);
            volumeSlider.RegisterValueChangedCallback(HandleVolumeChanged);
            cameraShakeSlider.RegisterValueChangedCallback(HandleCameraShakeChanged);
            fullscreenToggle.RegisterValueChangedCallback(HandleFullscreenChanged);
            vsyncToggle.RegisterValueChangedCallback(HandleVSyncChanged);

            LoadValues();
        }

        public void FocusPrimaryControl()
        {
            volumeSlider.Focus();
        }

        public void Dispose()
        {
            resolutionDropdown.UnregisterValueChangedCallback(HandleResolutionChanged);
            antiAliasingDropdown.UnregisterValueChangedCallback(HandleAntiAliasingChanged);
            volumeSlider.UnregisterValueChangedCallback(HandleVolumeChanged);
            cameraShakeSlider.UnregisterValueChangedCallback(HandleCameraShakeChanged);
            fullscreenToggle.UnregisterValueChangedCallback(HandleFullscreenChanged);
            vsyncToggle.UnregisterValueChangedCallback(HandleVSyncChanged);
        }

        private void LoadValues()
        {
            float volume = PlayerPrefs.GetFloat(VolumePreference, 1f);
            AudioListener.volume = volume;
            volumeSlider.SetValueWithoutNotify(volume);
            fullscreenToggle.SetValueWithoutNotify(VisualSettings.Fullscreen);
            vsyncToggle.SetValueWithoutNotify(VisualSettings.VSync);
            cameraShakeSlider.SetValueWithoutNotify(VisualSettings.CameraShakeIntensity);

            List<string> resolutionChoices = new List<string>(VisualSettings.ResolutionCount);
            for (int index = 0; index < VisualSettings.ResolutionCount; ++index)
            {
                resolutionChoices.Add(VisualSettings.GetResolutionLabel(index));
            }

            resolutionDropdown.choices = resolutionChoices;
            resolutionDropdown.index = VisualSettings.GetCurrentResolutionIndex();

            List<string> antiAliasingChoices = new List<string>(VisualSettings.AntiAliasingCount);
            for (int index = 0; index < VisualSettings.AntiAliasingCount; ++index)
            {
                antiAliasingChoices.Add(VisualSettings.GetAntiAliasingLabel(index));
            }

            antiAliasingDropdown.choices = antiAliasingChoices;
            antiAliasingDropdown.index = (int)VisualSettings.AntiAliasing;
        }

        private void HandleResolutionChanged(ChangeEvent<string> evt)
        {
            VisualSettings.SetResolution(resolutionDropdown.choices.IndexOf(evt.newValue));
        }

        private void HandleAntiAliasingChanged(ChangeEvent<string> evt)
        {
            VisualSettings.SetAntiAliasing(antiAliasingDropdown.choices.IndexOf(evt.newValue));
        }

        private static void HandleVolumeChanged(ChangeEvent<float> evt)
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(VolumePreference, evt.newValue);
            PlayerPrefs.Save();
        }

        private static void HandleCameraShakeChanged(ChangeEvent<float> evt)
        {
            VisualSettings.SetCameraShake(evt.newValue);
        }

        private static void HandleFullscreenChanged(ChangeEvent<bool> evt)
        {
            VisualSettings.SetFullscreen(evt.newValue);
        }

        private static void HandleVSyncChanged(ChangeEvent<bool> evt)
        {
            VisualSettings.SetVSync(evt.newValue);
        }
    }
}
