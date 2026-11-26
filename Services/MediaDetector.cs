using System.Runtime.InteropServices;

namespace LightBrightnessControl;

/// <summary>
/// 媒体播放检测器 - 检测是否有音频正在播放
/// 简化实现，避免 COM 异常导致程序崩溃
/// </summary>
public static class MediaDetector
{
    /// <summary>
    /// 检查是否有活动的音频会话（正在播放音频）
    /// 使用简化的检测方式，避免复杂的 COM 调用
    /// </summary>
    public static bool IsMediaPlaying()
    {
        // 暂时禁用音频检测功能，因为 COM 接口可能导致崩溃
        // 返回 false 表示没有检测到媒体播放
        // 如果需要此功能，可以考虑使用更稳定的检测方式
        return false;

        // 注意：原实现使用 Windows Audio Session API，
        // 但在某些系统配置下可能导致 COM 异常。
        // 如果需要启用，可以取消下面代码的注释并删除上面的 return false
        
        /*
        try
        {
            // 创建设备枚举器
            var enumerator = (NativeMethods.IMMDeviceEnumerator)new NativeMethods.MMDeviceEnumerator();

            // 获取默认音频输出设备
            int hr = enumerator.GetDefaultAudioEndpoint(
                NativeMethods.eRender,
                NativeMethods.eMultimedia,
                out var device);

            if (hr != 0 || device == null)
            {
                Marshal.ReleaseComObject(enumerator);
                return false;
            }

            // 激活音频会话管理器
            Guid iidAudioSessionManager2 = typeof(NativeMethods.IAudioSessionManager2).GUID;
            hr = device.Activate(iidAudioSessionManager2, 0x17, IntPtr.Zero, out object? sessionManagerObj);

            if (hr != 0 || sessionManagerObj == null)
            {
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);
                return false;
            }

            var sessionManager = (NativeMethods.IAudioSessionManager2)sessionManagerObj;

            // 获取会话枚举器
            hr = sessionManager.GetSessionEnumerator(out var sessionEnumerator);

            if (hr != 0 || sessionEnumerator == null)
            {
                Marshal.ReleaseComObject(sessionManager);
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);
                return false;
            }

            // 检查每个会话的状态
            sessionEnumerator.GetCount(out int sessionCount);
            bool isPlaying = false;

            for (int i = 0; i < sessionCount; i++)
            {
                sessionEnumerator.GetSession(i, out var session);
                if (session != null)
                {
                    session.GetState(out int state);
                    if (state == NativeMethods.AudioSessionStateActive)
                    {
                        isPlaying = true;
                        Marshal.ReleaseComObject(session);
                        break;
                    }
                    Marshal.ReleaseComObject(session);
                }
            }

            // 清理 COM 对象
            Marshal.ReleaseComObject(sessionEnumerator);
            Marshal.ReleaseComObject(sessionManager);
            Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);

            return isPlaying;
        }
        catch
        {
            // 如果检测失败，返回 false（不阻止闲置变暗）
            return false;
        }
        */
    }
}
