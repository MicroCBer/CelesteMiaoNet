﻿using Celeste.Mod.CelesteNet.DataTypes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MDraw = Monocle.Draw;

namespace Celeste.Mod.CelesteNet.Client {
    public class CelesteNetChatComponent : CelesteNetGameComponent {

        protected float _Time;

        public float Scale = 0.75f;

        protected Overlay _DummyOverlay = new Overlay();

        public List<DataChat> Log = new List<DataChat>();
        public string Typing = "";

        public List<string> Repeat = new List<string>() {
            ""
        };

        protected int _RepeatIndex = 0;
        public int RepeatIndex {
            get => _RepeatIndex;
            set {
                if (_RepeatIndex == value)
                    return;

                value = (value + Repeat.Count) % Repeat.Count;

                if (_RepeatIndex == 0 && value != 0)
                    Repeat[0] = Typing;
                Typing = Repeat[value];
                _RepeatIndex = value;
            }
        }

        protected bool _SceneWasPaused;
        protected int _ConsumeInput;
        protected bool _Active;
        public bool Active {
            get => _Active;
            set {
                if (_Active == value)
                    return;

                if (value) {
                    _SceneWasPaused = Engine.Scene.Paused;
                    Engine.Scene.Paused = true;
                    // If we're in a level, add a dummy overlay to prevent the pause menu from handling input.
                    if (Engine.Scene is Level level)
                        level.Overlay = _DummyOverlay;

                    _RepeatIndex = 0;
                    TextInput.OnInput += OnTextInput;

                } else {
                    Typing = "";
                    Engine.Scene.Paused = _SceneWasPaused;
                    _ConsumeInput = 2;
                    if (Engine.Scene is Level level && level.Overlay == _DummyOverlay)
                        level.Overlay = null;
                    TextInput.OnInput -= OnTextInput;
                }

                _Active = value;
            }
        }

        public CelesteNetChatComponent(CelesteNetClientComponent context, Game game)
            : base(context, game) {

            UpdateOrder = 10000;
            DrawOrder = 10001;
        }

        public void Handle(CelesteNetConnection con, DataChat msg) {
            if (!Log.Contains(msg))
                Log.Add(msg);
        }

        public override void Update(GameTime gameTime) {
            _Time += Engine.RawDeltaTime;

            if (!(Engine.Scene?.Paused ?? true)) {
                string typing = Typing;
                Active = false;
                Typing = typing;
            }

            if (!Active && Settings.ButtonChat.Button.Pressed) {
                Active = true;

            } else if (Active) {
                Engine.Commands.Open = false;

                if (MInput.Keyboard.Pressed(Keys.Enter)) {
                    // AAAAAAAAAAAAAAAAA
                    Active = false;

                } else if (MInput.Keyboard.Pressed(Keys.Down) && RepeatIndex > 0) {
                    RepeatIndex--;
                } else if (MInput.Keyboard.Pressed(Keys.Up) && RepeatIndex < Repeat.Count - 1) {
                    RepeatIndex++;

                } else if (Input.ESC.Pressed || Input.Pause.Pressed) {
                    Active = false;
                }
            }

            // Prevent menus from reacting to player input after exiting chat.
            if (_ConsumeInput > 0) {
                Input.MenuConfirm.ConsumeBuffer();
                Input.MenuConfirm.ConsumePress();
                Input.ESC.ConsumeBuffer();
                Input.ESC.ConsumePress();
                Input.Pause.ConsumeBuffer();
                Input.Pause.ConsumePress();
                _ConsumeInput--;
            }

        }

        public void OnTextInput(char c) {
            if (!Active)
                return;

            if (c == (char) 13) {
                // Enter - send.
                // Handled in Update.

            } else if (c == (char) 8) {
                // Backspace - trim.
                if (Typing.Length > 0)
                    Typing = Typing.Substring(0, Typing.Length - 1);
                RepeatIndex = 0;

            } else if (c == (char) 127) {
                // Delete - currenly not handled.

            } else if (!char.IsControl(c)) {
                // Any other character - append.
                Typing += c;
                RepeatIndex = 0;
            }
        }

        protected override void Render(GameTime gameTime, bool toBuffer) {
            float scale = Scale;
            Vector2 fontScale = Vector2.One * Scale;

            if (Active) {
                MDraw.Rect(25f * scale, UI_HEIGHT - 125f * scale, UI_WIDTH - 50f * scale, 100f * scale, Color.Black * 0.8f);

                string text = ">" + Typing;
                if (Calc.BetweenInterval(_Time, 0.5f))
                    text += "_";
                ActiveFont.Draw(
                    text,
                    new Vector2(50f * scale, UI_HEIGHT - 105f * scale),
                    Vector2.Zero,
                    fontScale,
                    Color.White
                );
            }

            lock (Log) {
                if (Log.Count > 0) {
                    DateTime now = DateTime.UtcNow;

                    float y = UI_HEIGHT - 50f * scale;
                    if (Active)
                        y -= 105f * scale;

                    for (int i = 0; i < Log.Count && i < Settings.ChatLogLength; i++) {
                        DataChat msg = Log[i];

                        float alpha = 1f;
                        float delta = (float) (now - msg.Date).TotalSeconds;
                        if (!Active && delta > 3f)
                            alpha = 1f - Ease.CubeIn(delta - 3f);
                        if (alpha <= 0f)
                            continue;

                        string text = msg.ToString();
                        Vector2 size = ActiveFont.Measure(text) * fontScale;
                        float height = 50f * scale + size.Y;

                        y -= height;

                        MDraw.Rect(25f * scale, y, size.X + 50f * scale, height, Color.Black * 0.8f * alpha);
                        ActiveFont.Draw(
                            text,
                            new Vector2(50f * scale, y + 25f * scale),
                            Vector2.Zero,
                            fontScale,
                            msg.Color * alpha
                        );
                    }
                }
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            TextInput.OnInput -= OnTextInput;
        }

    }
}