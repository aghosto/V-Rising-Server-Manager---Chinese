using System;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Drawing;

namespace VRisingServerManager
{
    // 自定义HwndHost用于嵌入外部窗口
    public class BepInExWindowHost : HwndHost
    {
        // 新增：保存窗口原始状态
        private struct WindowState
        {
            public IntPtr Parent; // 原始父窗口句柄
            public int Style;     // 原始窗口样式
            public int ExStyle;   // 原始扩展样式
            public Rectangle Bounds; // 原始位置和大小
        }

        // 导入必要的API（补充窗口样式操作）
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // 修正：使用uint版本的SetWindowLong（SetWindowLongPtr）
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);


        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // 导入GetWindowLongPtr（用于获取窗口样式）
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        // 修正：将常量改为uint类型（无符号整数）
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_CHILD = 0x40000000;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;


        private WindowState _originalState; // 保存嵌入前的窗口状态
        private IntPtr _bepInExWindowHandle; // BepInEx窗口句柄
        private IntPtr _hostHandle; // 宿主控件句柄

        // 根据平台选择合适的版本
        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        // 嵌入窗口方法（修改样式赋值部分）
        public void SetBepInExWindowHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return;

            _bepInExWindowHandle = windowHandle;

            // 保存原始样式（使用GetWindowLongPtr）
            _originalState.Style = (int)GetWindowLongPtr(windowHandle, GWL_STYLE);
            _originalState.ExStyle = (int)GetWindowLongPtr(windowHandle, GWL_EXSTYLE);

            // 修改样式（将uint转换为IntPtr）
            uint newStyle = (uint)_originalState.Style & ~WS_POPUP;
            newStyle |= WS_CHILD | WS_VISIBLE;
            SetWindowLongPtr(windowHandle, GWL_STYLE, (IntPtr)newStyle);


            // 3. 设置父窗口为宿主控件
            SetParent(windowHandle, _hostHandle);

            // 4. 强制窗口重绘并适应宿主大小
            UpdateWindowPosition();
        }

        // 分离窗口（恢复原始状态）
        public void ReleaseWindow()
        {
            if (_bepInExWindowHandle == IntPtr.Zero) return;

            // 1. 恢复原始父窗口
            SetParent(_bepInExWindowHandle, _originalState.Parent);

            // 2. 恢复原始样式
            SetWindowLong(_bepInExWindowHandle, GWL_STYLE, _originalState.Style);
            SetWindowLong(_bepInExWindowHandle, GWL_EXSTYLE, _originalState.ExStyle);

            // 3. 恢复原始位置和大小
            SetWindowPos(
                _bepInExWindowHandle,
                IntPtr.Zero,
                _originalState.Bounds.Left,
                _originalState.Bounds.Top,
                _originalState.Bounds.Width,
                _originalState.Bounds.Height,
                SWP_NOZORDER | SWP_FRAMECHANGED
            );

            // 4. 强制重绘
            SendMessage(_bepInExWindowHandle, WM_PAINT, IntPtr.Zero, IntPtr.Zero);

            _bepInExWindowHandle = IntPtr.Zero;
        }

        // 适配宿主控件大小（嵌入时调用）
        private void UpdateWindowPosition()
        {
            if (_bepInExWindowHandle == IntPtr.Zero || _hostHandle == IntPtr.Zero) return;

            // 获取宿主控件大小
            RECT hostRect;
            GetClientRect(_hostHandle, out hostRect);

            // 调整嵌入窗口大小以填充宿主
            SetWindowPos(
                _bepInExWindowHandle,
                IntPtr.Zero,
                0, 0,
                hostRect.Right - hostRect.Left,
                hostRect.Bottom - hostRect.Top,
                SWP_NOZORDER | SWP_NOMOVE | SWP_FRAMECHANGED
            );
        }

        // 宿主控件大小变化时，同步调整嵌入窗口大小
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateWindowPosition(); // 大小变化时更新窗口位置
        }

        // 导入补充API
        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_PAINT = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // 创建宿主控件句柄
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostHandle = CreateWindowEx(0, "static", "", 0x40000000 | 0x10000000, // WS_CHILD | WS_VISIBLE
                0, 0, 0, 0, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return new HandleRef(this, _hostHandle);
        }

        // 销毁宿主控件
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            ReleaseWindow(); // 释放外部窗口
            DestroyWindow(hwnd.Handle);
        }

        // 导入必要的API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int exStyle, string lpClassName, string lpWindowName, int style,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);
    }
}