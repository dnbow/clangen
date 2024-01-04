﻿using ClangenNET.UI;
using System;
using System.Collections.Generic;
using static SDL2.SDL;
using static SDL2.SDL_image;

namespace Clangen
{
    public struct Color
    {
        public uint Value;

        public Color(uint Value) 
        {
            this.Value = Value;
        }
        public Color(byte Red, byte Green, byte Blue, byte Alpha = 255)
        {
            Value = (uint)((Red << 24) | (Green << 16) | (Blue << 8) | Alpha);
        }

        public byte Red  => (byte)((Value & 0xFF000000) >> 24);
        public byte Green => (byte)((Value & 0xFF0000) >> 16);
        public byte Blue  => (byte)((Value & 0xFF00) >> 8);
        public byte Alpha => (byte)(Value & 0xFF);

        public static implicit operator Color(int Value) => new Color((uint)Value);
    }
    public class ThemeState
    {
        public bool IsDarkMode;

        public Color BackgroundLight = new Color(255, 255, 255);
        public Color BackgroundDark = new Color(0, 0, 0);

        public Color Background => IsDarkMode ? BackgroundDark : BackgroundLight;
    }


    public class GameState
    {
        public IntPtr Window;
        public IntPtr Renderer;
        public byte RenderState;

        public Dictionary<string, IntPtr> ImageCache;
        public ThemeState Theme = new ThemeState();

        public bool isRunning = false;
        public bool isFullscreen = false;

        public int ScreenWidth  { get; set; } = 640;
        public int ScreenHeight { get; set; } = 480;

        public List<BaseScreen> Screens = new List<BaseScreen>();
        public BaseScreen CurrentScreen;
        public BaseScreen LastScreen;


        public List<BaseElement> HoveredElements = new List<BaseElement>();


        public GameState() { 
            ImageCache = new Dictionary<string, IntPtr>();
        }



        public int Init()
        {
            SDL_SetHint(SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
            if (SDL_Init(SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine($"There was an issue initilizing SDL. {SDL_GetError()}");
                return 10;
            }

            Window = SDL_CreateWindow("ClangenNET", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, 800, 700, SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (Window == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue creating the window. {SDL_GetError()}");
                return 11;
            }

            Renderer = SDL_CreateRenderer(Window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            if (Renderer == IntPtr.Zero)
            {
                Console.WriteLine($"There was an issue creating the renderer. {SDL_GetError()}");
                return 12;
            }

            if (IMG_Init(IMG_InitFlags.IMG_INIT_PNG) == 0)
            {
                Console.WriteLine($"There was an issue initilizing SDL2_Image {IMG_GetError()}");
                return 13;
            }

            SDL_SetRenderDrawColor(Renderer, 135, 206, 235, 255);
            return 0;
        }



        public int Load()
        {
            return 0;
        }



        public int Finalise()
        {
            Screens = new List<BaseScreen> {
                new ClangenNET.Screens.StartScreen()
            };
            return 0;
        }



        public void Quit()
        {
            isRunning = false;
            SDL_DestroyRenderer(Renderer);
            SDL_DestroyWindow(Window);
            SDL_Quit();
        }



        public void Render(int maxState = 0)
        {
            switch (RenderState | maxState)
            {
                case 1: // Just present
                    SDL_RenderPresent(Renderer);
                    break;
                case 2: // Rebuild current screen
                    var Background = Theme.Background;
                    SDL_SetRenderDrawColor(Renderer, Background.Red, Background.Green, Background.Blue, Background.Alpha);
                    SDL_RenderClear(Renderer);
                    CurrentScreen.Rebuild(this);
                    SDL_RenderPresent(Renderer);
                    break;
                default:
                    break;
            }

            RenderState = 0;
        }



        public void Update()
        {
            while (SDL_PollEvent(out SDL_Event Event) == 1)
            {
                switch (Event.type)
                {
                    case SDL_EventType.SDL_QUIT:
                        isRunning = false;
                        break;

                    case SDL_EventType.SDL_KEYUP:
                        switch (Event.key.keysym.sym)
                        {
                            case SDL_Keycode.SDLK_F10: // Toggle darkmode
                                Theme.IsDarkMode = !Theme.IsDarkMode;
                                RenderState |= 1;
                                break;

                            case SDL_Keycode.SDLK_F11:
                                SetFullscreen(!isFullscreen);
                                break;

                            case SDL_Keycode.SDLK_F12:
                                SDL_GetRendererOutputSize(Renderer, out int W, out int H);
                                IntPtr Screenshot = SDL_CreateRGBSurface(0, W, H, 32, 0x000000FF, 0x0000FF00, 0x00FF0000, 0xFF000000);

                                unsafe {
                                    SDL_Surface * Surface = (SDL_Surface *) Screenshot;
                                    SDL_Rect rect = new SDL_Rect { x = 0, y = 0, w = ScreenWidth, h = ScreenHeight };

                                    SDL_LockSurface(Screenshot);
                                    SDL_RenderReadPixels(SDL_GetRenderer(Window), ref rect, ((SDL_PixelFormat*)(Surface->format))->format, Surface->pixels, Surface->pitch);
                                    SDL_UnlockSurface(Screenshot);
                                    SDL_FreeSurface(Screenshot);
                                }
                                break;
                                
                        }
                        break;

                    case SDL_EventType.SDL_KEYDOWN:
                        break;

                    case SDL_EventType.SDL_MOUSEMOTION:
                        SDL_GetMouseState(out int MouseX, out int MouseY);

                        for (int i = 0; i < CurrentScreen.Elements.Count; i++)
                        {
                            var Element = CurrentScreen.Elements[i];
                            if (Element.Enabled)
                            {
                                SDL_Rect rect = Element.Rect;
                                bool isHovered = rect.x < MouseX && rect.y < MouseY && MouseX <= (rect.w + rect.x) && MouseY <= (rect.h + rect.y);

                                if (isHovered && !Element.Hovered)
                                {
                                    Element.Hovered = true;
                                    if (Element.OnHover != null)
                                        Element.OnHover(this);
                                    HoveredElements.Add(Element);
                                    RenderState |= 1;
                                }
                                else if (!isHovered && Element.Hovered)
                                {
                                    Element.Hovered = false;
                                    if (Element.OnHoverOff != null) 
                                        Element.OnHoverOff(this);
                                    if (HoveredElements.Contains(Element))
                                        HoveredElements.Remove(Element);
                                    RenderState |= 1;
                                }
                            }
                        }

                        break;

                    case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        if (Event.button.button == SDL_BUTTON_LEFT)
				            CurrentScreen.OnClick(this);

                        for (int i = 0; i < HoveredElements.Count; i++)
                        {
                            var Element = HoveredElements[i];
                            if (Element.OnClick != null)
                                Element.OnClick(this);
                        }

			            break;

                    default:
                        break;
                }
            }
        }



        public IntPtr GetTexture(string Identifier, bool Cache = false)
        {
            ImageCache.TryGetValue(Identifier, out IntPtr Texture);
            Texture = Texture != default ? Texture : IMG_LoadTexture(Renderer, $"Common\\{Identifier}");

            if (Texture == null)
                Console.Write(IMG_GetError());

            if (Cache)
                ImageCache[Identifier] = Texture;
            else if (ImageCache.ContainsKey(Identifier))
                ImageCache.Remove(Identifier);

            return Texture;
        }



        public void SetFullscreen(bool state)
        {
            isFullscreen = state;

            if (state)
            {
                SDL_SetWindowFullscreen(Window, 0);
            }
            else
            {
                SDL_SetWindowFullscreen(Window, (uint)SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP);
            }

            RenderState |= 2;
        }



        public void SetScreen(BaseScreen NewScreen)
        {
            SDL_RenderClear(Renderer);

            if (CurrentScreen != null)
            {
                CurrentScreen.OnClose(this);
                HoveredElements.RemoveAll(CurrentScreen.Elements.Contains);
            }

            LastScreen = CurrentScreen;
            CurrentScreen = NewScreen;

            CurrentScreen.OnOpen(this);
            RenderState |= 2;
        }
    } 
}
