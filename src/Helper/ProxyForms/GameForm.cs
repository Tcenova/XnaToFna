﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDL2;
using System;
using System.Collections.Generic;

namespace XnaToFna.ProxyForms {
    public sealed class GameForm : Form {

        public static GameForm Instance;

        private bool _Dirty = false;
        private bool Dirty {
            get {
                return _Dirty;
            }
            set {
                if (value) {
                    _FormBorderStyle = FormBorderStyle;
                    _WindowState = WindowState;
                }

                _Dirty = value;
            }
        }

        private bool FakeFullscreenWindow = false;

        private Rectangle _WindowedBounds = new Rectangle();
        private Rectangle _Bounds = new Rectangle();
        public override ProxyDrawing.Rectangle Bounds {
            get {
                return new ProxyDrawing.Rectangle(
                    _Bounds.X,
                    _Bounds.Y,
                    _Bounds.Width,
                    _Bounds.Height
                );
            }
            set {
                SDLBounds = _Bounds = _WindowedBounds = new Rectangle(
                    value.X,
                    value.Y,
                    value.Width,
                    value.Height
                );
            }
        }

        public Rectangle SDLBounds {
            get {
                return XnaToFnaHelper.Game.Window.ClientBounds;
            }
            set {
                IntPtr window = XnaToFnaHelper.Game.Window.Handle;
                SDL.SDL_SetWindowSize(window, value.Width, value.Height);
                SDL.SDL_SetWindowPosition(window, value.X, value.Y);
            }
        }

        protected override ProxyDrawing.Rectangle _ClientRectangle {
            get {
                Rectangle bounds = XnaToFnaHelper.Game.Window.ClientBounds;
                return new ProxyDrawing.Rectangle(
                    0,
                    0,
                    bounds.Width,
                    bounds.Height
                );
            }
        }

        public override ProxyDrawing.Point Location {
            get {
                int x, y;
                IntPtr window = XnaToFnaHelper.Game.Window.Handle;
                SDL.SDL_GetWindowPosition(window, out x, out y);
                return new ProxyDrawing.Point(x, y);
            }
            set {
                IntPtr window = XnaToFnaHelper.Game.Window.Handle;
                SDL.SDL_SetWindowPosition(window, value.X, value.Y);
            }
        }


        private FormBorderStyle _FormBorderStyle = FormBorderStyle.FixedDialog;
        public override FormBorderStyle FormBorderStyle {
            get {
                if (Dirty)
                    return _FormBorderStyle;
                if (XnaToFnaHelper.Game.Window.IsBorderlessEXT || FakeFullscreenWindow)
                    return FormBorderStyle.None;
                if (XnaToFnaHelper.Game.Window.AllowUserResizing)
                    return FormBorderStyle.Sizable;
                return FormBorderStyle.FixedDialog;
            }
            set {
                Dirty = true;
                _FormBorderStyle = value;
            }
        }

        private FormWindowState _WindowState = FormWindowState.Normal;
        public override FormWindowState WindowState {
            get {
                if (Dirty)
                    return _WindowState;
                uint flags = SDL.SDL_GetWindowFlags(XnaToFnaHelper.Game.Window.Handle);
                if ((flags & (uint) SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0 || FakeFullscreenWindow)
                    return FormWindowState.Maximized;
                if ((flags & (uint) SDL.SDL_WindowFlags.SDL_WINDOW_MINIMIZED) != 0)
                    return FormWindowState.Minimized;
                return FormWindowState.Normal;
            }
            set {
                Dirty = true;
                _WindowState = value;
            }
        }

        private FormStartPosition _StartPosition = FormStartPosition.WindowsDefaultLocation;
        public override FormStartPosition StartPosition {
            get {
                return _StartPosition;
            }
            set {
                if ((SDL.SDL_GetWindowFlags(XnaToFnaHelper.Game.Window.Handle) & (uint) SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN) != (uint) SDL.SDL_WindowFlags.SDL_WINDOW_HIDDEN)
                    // Window not hidden anymore, this won't change the "start" position.
                    return;
                // Setting Dirty = true isn't required (yet) - this is just manipulating the window position.
                switch (value) {
                    case FormStartPosition.CenterParent:
                    case FormStartPosition.CenterScreen:
                        SDL.SDL_SetWindowPosition(XnaToFnaHelper.Game.Window.Handle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);
                        break;
                    case FormStartPosition.WindowsDefaultBounds:
                        // Inaccurate because the window bounds aren't "system default."
                    case FormStartPosition.WindowsDefaultLocation:
                        SDL.SDL_SetWindowPosition(XnaToFnaHelper.Game.Window.Handle, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED);
                        break;
                    case FormStartPosition.Manual:
                        // Setting Location is enough.
                        break;
                }
                _StartPosition = value;
            }
        }

        public override bool Focused {
            get {
                return XnaToFnaHelper.Game.IsActive;
            }
        }

        protected override void SetVisibleCore(bool visible) {
            // TODO: Invoke SetVisibleCore from XnaToFna. Games can override this.
        }


        public void SDLWindowSizeChanged(object sender, EventArgs e) {
            Rectangle sdlBounds = SDLBounds;
            _Bounds = new Rectangle(sdlBounds.X, sdlBounds.Y, sdlBounds.Width, sdlBounds.Height);

            if ((SDL.SDL_GetWindowFlags(XnaToFnaHelper.Game.Window.Handle) & (uint) SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) == 0 && !FakeFullscreenWindow)
                _WindowedBounds = _Bounds;
        }

        public void SDLWindowChanged(
            IntPtr window,
            int clientWidth,
            int clientHeight,
            bool wantsFullscreen,
            string screenDeviceName,
            ref string resultDeviceName
        ) {
            SDLWindowSizeChanged(null, null);
        }

        protected override void _Close() {
            XnaToFnaHelper.Game.Exit();
        }


        public void ApplyChanges() {
            if (!Dirty || Environment.GetEnvironmentVariable("FNADROID") == "1")
                return;

            XnaToFnaGame game = XnaToFnaHelper.Game;
            IntPtr window = game.Window.Handle;
            GraphicsDeviceManager gdm = XnaToFnaHelper.GetService<IGraphicsDeviceManager, GraphicsDeviceManager>();
            bool fullscreen = gdm.IsFullScreen;

            bool borderless = FormBorderStyle == FormBorderStyle.None;
            bool maximized = WindowState == FormWindowState.Maximized;
            bool wasFakeFullscreenWindow = FakeFullscreenWindow;
            FakeFullscreenWindow = maximized && borderless;

            XnaToFnaHelper.Log("[ProxyForms] Applying changes from ProxyForms.Form to SDL window");
            XnaToFnaHelper.Log($"[ProxyForms] Currently fullscreen: {fullscreen}; Fake fullscreen window: {FakeFullscreenWindow}; Border: {FormBorderStyle}; State: {WindowState}");

            if (FakeFullscreenWindow) {
                XnaToFnaHelper.Log("[ProxyForms] Game expects borderless fullscreen... give it proper fullscreen instead.");

                if (!fullscreen)
                    _WindowedBounds = SDLBounds;

                XnaToFnaHelper.Log($"[ProxyForms] Last window size: {_WindowedBounds.Width} x {_WindowedBounds.Height}");

                DisplayMode dm = gdm.GraphicsDevice.DisplayMode;
                // This feels so wrong.
                gdm.PreferredBackBufferWidth = dm.Width;
                gdm.PreferredBackBufferHeight = dm.Height;
                gdm.IsFullScreen = true;
                gdm.ApplyChanges();

                _Bounds = SDLBounds;

            } else {
                if (wasFakeFullscreenWindow) {
                    XnaToFnaHelper.Log("[ProxyForms] Leaving fake borderless fullscreen.");
                    gdm.IsFullScreen = false;
                }

                // Shows the ugly title bar on Android
                game.Window.IsBorderlessEXT = borderless;

                if (maximized) {
                    SDL.SDL_MaximizeWindow(window);
                    _Bounds = SDLBounds;
                } else {
                    SDL.SDL_RestoreWindow(window);
                    SDLBounds = _Bounds = _WindowedBounds;
                }

                // This also feels so wrong.
                XnaToFnaHelper.Log($"[ProxyForms] New window size: {_Bounds.Width} x {_Bounds.Height}");
                gdm.PreferredBackBufferWidth = _Bounds.Width;
                gdm.PreferredBackBufferHeight = _Bounds.Height;
                gdm.ApplyChanges();
            }

            Dirty = false;
        }

    }

}
