using System;
using System.Collections.Generic;
using System.Text;

namespace EasySave.Gui.Audiodescription.IViewModel
{
    internal interface IAudiodescription
    {
        void SetVolume(double volume);
        void ServiceStatement(bool onOff);
        void Start();
        void Stop();

        double GetVolume();
    }
}
