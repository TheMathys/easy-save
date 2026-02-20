using EasySave.Gui.Audiodescription.IViewModel;
using System.Speech.Synthesis;

namespace EasySave.Gui.Audiodescription.Service
{
    internal class Audiodescription : IAudiodescription
    {
        private double Volume { get; set; } = 100;
        private SpeechSynthesizer _synthesizer;
        private bool _enabled;

        /// <summary>
        /// Audiodescription.Start() will begin the service.
        /// Initializes the speech synthesizer and prepares it for use.
        /// </summary>
        public void Start()
        {
            if (_enabled)
                return;

            _enabled = true;

            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.Volume = (int)Volume;
        }

        /// <summary>
        /// Audiodescription.Stop() will stop the service.
        /// Cancels any speech and disposes the synthesizer.
        /// </summary>
        public void Stop()
        {
            if (!_enabled)
                return;

            _enabled = false;

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
            if (_enabled && _synthesizer != null)
            {
                _synthesizer.SpeakAsync(text);
            }
        }

        /// <summary>
        /// Audiodescription.SetVolume() sets the volume level.
        /// </summary>
        public void SetVolume(double volume)
        {
            Volume = volume;

            if (_synthesizer != null)
                _synthesizer.Volume = (int)Volume;
        }

        /// <summary>
        /// Audiodescription.GetVolume() returns the current volume.
        /// </summary>
        public double GetVolume()
        {
            return Volume;
        }
    }
}