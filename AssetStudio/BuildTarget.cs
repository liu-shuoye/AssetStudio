using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 构建目标枚举，用于指定项目构建的目标平台。 </summary>
    public enum BuildTarget
    {
        /// <summary>  未指定目标。 </summary>
        NoTarget = -2,

        /// <summary> 任意玩家端。 </summary>
        AnyPlayer = -1,

        /// <summary> 合法的玩家端。 </summary>
        ValidPlayer = 1,

        /// <summary> 旧版Mac OS X平台。 </summary>
        StandaloneOSX = 2,

        /// <summary> 旧版Mac OS X PowerPC架构平台。 </summary>
        StandaloneOSXPPC = 3,

        /// <summary> Mac OS X Intel架构平台。 </summary>
        StandaloneOSXIntel = 4,

        /// <summary> Windows独立版。 </summary>
        StandaloneWindows,

        /// <summary> Web Player，已废弃。 </summary>
        WebPlayer,

        /// <summary> Web Player流式传输，已废弃。 </summary>
        WebPlayerStreamed,

        /// <summary> Wii平台。 </summary>
        Wii = 8,

        /// <summary> iOS平台。 </summary>
        iOS = 9,

        /// <summary> PlayStation 3平台。 </summary>
        PS3,

        /// <summary> Xbox 360平台。 </summary>
        XBOX360,

        /// <summary> Broadcom平台，主要用于嵌入式设备。 </summary>
        Broadcom = 12,

        /// <summary> Android平台。 </summary>
        Android = 13,

        /// <summary> GLES仿真器独立版。 </summary>
        StandaloneGLESEmu = 14,

        /// <summary> GLES 2.0仿真器独立版。 </summary>
        StandaloneGLES20Emu = 15,

        /// <summary> Native Client (NaCl)平台。 </summary>
        NaCl = 16,

        /// <summary> Linux独立版。 </summary>
        StandaloneLinux = 17,

        /// <summary> Flash Player。 </summary>
        FlashPlayer = 18,

        /// <summary> Windows 64位独立版。 </summary>
        StandaloneWindows64 = 19,

        /// <summary> WebGL。 </summary>
        WebGL,

        /// <summary> Windows Store应用。 </summary>
        WSAPlayer,

        /// <summary> Linux 64位独立版。 </summary>
        StandaloneLinux64 = 24,

        /// <summary> Linux通用版，包含多种架构。 </summary>
        StandaloneLinuxUniversal,

        /// <summary> Windows Phone 8平台。 </summary>
        WP8Player,

        /// <summary> Mac OS X Intel 64位架构平台。 </summary>
        StandaloneOSXIntel64,

        /// <summary> BlackBerry平台。 </summary>
        BlackBerry,

        /// <summary> Tizen平台。 </summary>
        Tizen,

        /// <summary> PlayStation Vita平台。 </summary>
        PSP2,

        /// <summary> PlayStation 4平台。 </summary>
        PS4,

        /// <summary> PlayStation Mobile平台。 </summary>
        PSM,

        /// <summary> Xbox One平台。 </summary>
        XboxOne,

        /// <summary> Samsung TV平台。 </summary>
        SamsungTV,

        /// <summary> Nintendo 3DS平台。 </summary>
        N3DS,

        /// <summary> Wii U平台。 </summary>
        WiiU,

        /// <summary> Apple tvOS平台。 </summary>
        tvOS,

        /// <summary> Nintendo Switch平台。 </summary>
        Switch,

        /// <summary> Magic Leap平台。 </summary>
        Lumin,

        /// <summary> Google Stadia平台。 </summary>
        Stadia,

        /// <summary> 云渲染平台。 </summary>
        CloudRendering,

        /// <summary> Xbox Series平台。 </summary>
        GameCoreXboxSeries,

        /// <summary> Xbox One平台（GameCore）。 </summary>
        GameCoreXboxOne,

        /// <summary> PlayStation 5平台。 </summary>
        PS5,

        /// <summary> 嵌入式Linux平台。 </summary>
        EmbeddedLinux,

        /// <summary> QNX平台，常用于嵌入式系统。 </summary>
        QNX,

        /// <summary> 未知平台。 </summary>
        UnknownPlatform = 9999
    }
}