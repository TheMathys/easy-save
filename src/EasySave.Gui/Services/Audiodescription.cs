using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Speech.Synthesis;

namespace EasySave.Gui.Services
{
    public class Audiodescription : IAudiodescription
    {
        private int Volume { get; set; } = 100;
        private SpeechSynthesizer _synthesizer;
        private bool _enabled;
        private Window? _mainWindow;
        private Control? _lastHoveredControl;

        /// <summary>
        /// Audiodescription.Start() will begin the service.
        /// Initializes the speech synthesizer and attaches event handlers for dynamic reading.
        /// </summary>
        public void Start()
        {
            if (_enabled)
                return;

            _enabled = true;

            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.Volume = (int)Volume;

            AttachToMainWindow();
        }

        /// <summary>
        /// Attaches event handlers to the main window for dynamic element tracking.
        /// </summary>
        private void AttachToMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                _mainWindow = desktop.MainWindow;
                
                if (_mainWindow != null)
                {
                    _mainWindow.PointerMoved += OnPointerMoved;
                }
            }
        }

        /// <summary>
        /// Handles pointer movement to detect hovered controls and read their content.
        /// </summary>
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_enabled || _mainWindow == null)
                return;

            Point position = e.GetPosition(_mainWindow);
            var visual = _mainWindow.InputHitTest(position);

            if (visual is Control control && control != _lastHoveredControl)
            {
                _lastHoveredControl = control;
                ReadControlContent(control);
            }
        }

        /// <summary>
        /// Reads the content of a control based on its type and properties.
        /// Specifically handles Slider for volume and ToggleSwitch for activation.
        /// </summary>
        private void ReadControlContent(Control control)
        {
            string textToRead = string.Empty;

            // Extract text based on control type
            switch (control)
            {
                case Slider slider:
                    string sliderName = !string.IsNullOrWhiteSpace(slider.Name) ? slider.Name : "Curseur";
                    double sliderValue = Math.Round(slider.Value, 0);
                    textToRead = $"{sliderName}: {sliderValue} pourcent";
                    break;

                case ToggleSwitch toggleSwitch:
                    string toggleName = !string.IsNullOrWhiteSpace(toggleSwitch.Content?.ToString()) 
                        ? toggleSwitch.Content.ToString() : "Interrupteur";
                    string toggleState = toggleSwitch.IsChecked == true ? "activé" : "désactivé";
                    textToRead = $"{toggleName}: {toggleState}";
                    break;

                case TextBlock textBlock:
                    textToRead = textBlock.Text ?? string.Empty;
                    break;

                case Button button:
                    textToRead = $"Bouton {button.Content?.ToString() ?? "sans titre"}";
                    break;

                case TextBox textBox:
                    string textBoxName = !string.IsNullOrWhiteSpace(textBox.Name) ? textBox.Name : "Zone de texte";
                    textToRead = $"{textBoxName}: {textBox.Text ?? "vide"}";
                    break;
                case ComboBox comboBox:
                    string comboName = !string.IsNullOrWhiteSpace(comboBox.Name) ? comboBox.Name : "Liste déroulante";
                    textToRead = $"{comboName}: {comboBox.SelectedItem?.ToString() ?? "aucune sélection"}";
                    break;

                case ListBoxItem listBoxItem:
                    textToRead = listBoxItem.Content?.ToString() ?? "Élément de liste";
                    break;

                default:
                    // Try to get tooltip or name as fallback
                    if (ToolTip.GetTip(control) is string tooltip && !string.IsNullOrWhiteSpace(tooltip))
                        textToRead = tooltip;
                    else if (!string.IsNullOrWhiteSpace(control.Name))
                        textToRead = control.Name;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(textToRead))
            {
                SpeakText(textToRead);
            }
        }

        /// <summary>
        /// Detaches event handlers from the main window.
        /// </summary>
        private void DetachFromMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.PointerMoved -= OnPointerMoved;
                _mainWindow = null;
            }
            _lastHoveredControl = null;
        }

        /// <summary>
        /// Audiodescription.Stop() will stop the service.
        /// Cancels any speech, detaches event handlers, and disposes the synthesizer.
        /// </summary>
        public void Stop()
        {
            if (!_enabled)
                return;

            _enabled = false;

            DetachFromMainWindow();
            _synthesizer?.SpeakAsyncCancelAll();
            _synthesizer?.Dispose();
            _synthesizer = null;
        }

        /// <summary>
        /// Audiodescription.ServiceStatement() starts or stops the service
        /// depending on the boolean parameter.
        /// </summary>
        public void ServiceStatement(bool onOff)
        {
            if (onOff)
                Start();
            else
                Stop();
        }

        /// <summary>
        /// Audiodescription.SpeakText() will speak the given text
        /// if the service is enabled.
        /// </summary>
        public void SpeakText(string text)
        {
            if (_enabled && _synthesizer != null && !string.IsNullOrWhiteSpace(text))
            {
                // Cancel previous speech to avoid overlap
                _synthesizer.SpeakAsyncCancelAll();
                _synthesizer.SpeakAsync(text);
            }
        }

        /// <summary>
        /// Audiodescription.SetVolume() sets the volume level.
        /// </summary>
        public void SetVolume(int volume)
        {
            Volume = volume;

            if (_synthesizer != null)
                _synthesizer.Volume = Volume;
        }

        /// <summary>
        /// Audiodescription.GetVolume() returns the current volume.
        /// </summary>
        public int GetVolume()
        {
            return Volume;
        }
    }
}