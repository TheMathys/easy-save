using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;

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
            _synthesizer.Volume = Volume;

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
                    int sliderValue = (int)slider.Value;
                    textToRead = $"{sliderName}: {sliderValue} pourcent";
                    break;

                case ToggleSwitch toggleSwitch:
                    // Only read the text content, not the control itself
                    if (toggleSwitch.Content is string content && !string.IsNullOrWhiteSpace(content))
                    {
                        string toggleState = toggleSwitch.IsChecked == true ? "activé" : "désactivé";
                        textToRead = $"{content}: {toggleState}";
                    }
                    break;

                case TextBlock textBlock:
                    // Only read if there's actual text content
                    if (!string.IsNullOrWhiteSpace(textBlock.Text))
                    {
                        textToRead = textBlock.Text;
                    }
                    break;

                case Button button:
                    // Only read the text content of the button
                    if (button.Content is string buttonContent && !string.IsNullOrWhiteSpace(buttonContent))
                    {
                        textToRead = $"Bouton {buttonContent}";
                    }
                    break;

                case TextBox textBox:
                    // Only read if there's text in the textbox
                    if (!string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        string textBoxName = !string.IsNullOrWhiteSpace(textBox.Name) ? textBox.Name : "Zone de texte";
                        textToRead = $"{textBoxName}: {textBox.Text}";
                    }
                    break;

                case ComboBox comboBox:
                    // Only read if there's a selection
                    if (comboBox.SelectedItem != null)
                    {
                        string comboName = !string.IsNullOrWhiteSpace(comboBox.Name) ? comboBox.Name : "Liste déroulante";
                        textToRead = $"{comboName}: {comboBox.SelectedItem.ToString()}";
                    }
                    break;

                case ListBoxItem listBoxItem:
                    // Only read the text content
                    if (listBoxItem.Content is string itemContent && !string.IsNullOrWhiteSpace(itemContent))
                    {
                        textToRead = itemContent;
                    }
                    break;

                default:
                    // Only use tooltip if explicitly set
                    if (ToolTip.GetTip(control) is string tooltip && !string.IsNullOrWhiteSpace(tooltip))
                    {
                        textToRead = tooltip;
                    }
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

                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Cancel previous speech to avoid overlap
                    _synthesizer.SpeakAsyncCancelAll();
                    _synthesizer.SpeakAsync(text);
                }
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