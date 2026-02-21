﻿using System;
using System.Collections.Generic;
using System.Text;

namespace EasySave.Gui.Services
{
    public interface IAudiodescription
    {
        void SetVolume(int volume);
        void ServiceStatement(bool onOff);
        void Start();
        void Stop();

        int GetVolume();
    }
}
