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

namespace Celeste.Mod.CelesteNet.Client.Components {
    public class CelesteNetPlayerListComponent : CelesteNetGameComponent {

        public float Scale => Settings.UIScale;

        public readonly Color ColorChannelHeader = Calc.HexToColor("DDDD88");
        public readonly Color ColorChannelHeaderOwn = Calc.HexToColor("FFFF77");
        public readonly Color ColorChannelHeaderPrivate = Calc.HexToColor("CCCC22") * 0.6f;

        public bool Active;

        private List<Blob> List = new List<Blob>();

        public DataChannelList Channels;

        public ListMode Mode => Settings.PlayerListMode;
        private ListMode LastMode;

        public enum ListMode {
            Channels,
            Classic,
        }

        public CelesteNetPlayerListComponent(CelesteNetClientContext context, Game game)
            : base(context, game) {

            UpdateOrder = 10000;
            DrawOrder = 10001;
        }

        public void RebuildList() {
            if (MDraw.DefaultFont == null || Client == null || Channels == null)
                return;

            List<Blob> list = new List<Blob>();

            StringBuilder builder = new StringBuilder();


            switch (Mode) {
                case ListMode.Classic:
                    foreach (DataPlayerInfo player in Client.Data.GetRefs<DataPlayerInfo>()) {
                        if (string.IsNullOrWhiteSpace(player.DisplayName))
                            continue;

                        builder.Append(player.DisplayName);

                        DataChannelList.Channel channel = Channels.List.FirstOrDefault(c => c.Players.Contains(player.ID));
                        if (channel != null && !string.IsNullOrEmpty(channel.Name)) {
                            builder
                                .Append(" #")
                                .Append(channel.Name);
                        }

                        if (Client.Data.TryGetBoundRef(player, out DataPlayerState state))
                            AppendState(builder, state);

                        builder.AppendLine();
                    }

                    list.Add(new Blob {
                        Text = builder.ToString().Trim(),
                        ScaleFactor = 1f
                    });
                    break;

                case ListMode.Channels:
                    HashSet<DataPlayerInfo> listed = new HashSet<DataPlayerInfo>();

                    DataChannelList.Channel own = Channels.List.FirstOrDefault(c => c.Players.Contains(Client.PlayerInfo.ID));

                    if (own != null) {
                        list.Add(new Blob {
                            Text = own.Name,
                            Color = ColorChannelHeaderOwn
                        });

                        builder.Clear();
                        foreach (uint playerID in own.Players) 
                            listed.Add(ListPlayerUnderChannel(builder, playerID));
                        list.Add(new Blob {
                            Text = builder.ToString().Trim(),
                            ScaleFactor = 0.5f
                        });
                    }

                    foreach (DataChannelList.Channel channel in Channels.List) {
                        if (channel == own)
                            continue;

                        list.Add(new Blob {
                            Text = channel.Name,
                            Color = ColorChannelHeader
                        });

                        builder.Clear();
                        foreach (uint playerID in channel.Players)
                            listed.Add(ListPlayerUnderChannel(builder, playerID));
                        list.Add(new Blob {
                            Text = builder.ToString().Trim(),
                            ScaleFactor = 1f
                        });
                    }

                    bool wrotePrivate = false;

                    builder.Clear();
                    foreach (DataPlayerInfo player in Client.Data.GetRefs<DataPlayerInfo>()) {
                        if (listed.Contains(player) || string.IsNullOrWhiteSpace(player.DisplayName))
                            continue;

                        if (!wrotePrivate) {
                            wrotePrivate = true;
                            list.Add(new Blob {
                                Text = "!<private>",
                                Color = ColorChannelHeaderPrivate
                            });
                        }

                        builder.AppendLine(player.DisplayName);
                    }

                    if (wrotePrivate) {
                        list.Add(new Blob {
                            Text = builder.ToString().Trim(),
                            ScaleFactor = 1f
                        });
                    }
                    break;
            }

            List = list;
        }

        private DataPlayerInfo ListPlayerUnderChannel(StringBuilder builder, uint playerID) {
            if (Client.Data.TryGetRef(playerID, out DataPlayerInfo player) && !string.IsNullOrEmpty(player.DisplayName)) {
                builder
                    .Append(player.DisplayName);

                if (Client.Data.TryGetBoundRef(player, out DataPlayerState state))
                    AppendState(builder, state);

                builder.AppendLine();
                return player;

            } else {
                builder.AppendLine("  ?");
                return null;
            }
        }

        private void AppendState(StringBuilder builder, DataPlayerState state) {
            if (!string.IsNullOrWhiteSpace(state.SID))
                builder
                    .Append(" @ ")
                    .Append(AreaDataExt.Get(state.SID)?.Name?.DialogCleanOrNull(Dialog.Languages["english"]) ?? state.SID)
                    .Append(" ")
                    .Append((char) ('A' + (int) state.Mode))
                    .Append(" ")
                    .Append(state.Level);

            if (state.Idle)
                builder.Append(" :celestenet_idle:");
        }

        public void Handle(CelesteNetConnection con, DataPlayerInfo info) {
            RebuildList();
        }

        public void Handle(CelesteNetConnection con, DataPlayerState state) {
            RebuildList();
        }

        public void Handle(CelesteNetConnection con, DataChannelList channels) {
            Channels = channels;
            RebuildList();
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            if (LastMode != Mode) {
                LastMode = Mode;
                RebuildList();
            }

            if (!(Engine.Scene?.Paused ?? false) && Settings.ButtonPlayerList.Button.Pressed)
                Active = !Active;
        }

        public override void Draw(GameTime gameTime) {
            if (Active)
                base.Draw(gameTime);
        }

        protected override void Render(GameTime gameTime, bool toBuffer) {
            float scale = Scale;

            float y = 50f * scale;

            SpeedrunTimerDisplay timer = Engine.Scene?.Entities.FindFirst<SpeedrunTimerDisplay>();
            if (timer != null) {
                switch (global::Celeste.Settings.Instance?.SpeedrunClock ?? SpeedrunType.Off) {
                    case SpeedrunType.Off:
                        break;

                    case SpeedrunType.Chapter:
                        if (timer.CompleteTimer < 3f)
                            y += 104f;
                        break;

                    case SpeedrunType.File:
                    default:
                        y += 104f + 24f;
                        break;
                }
            }

            List<Blob> list = List;

            int textScaleTry = 0;
            float textScale = scale;
            RetryLineScale:

            Vector2 sizeAll = Vector2.Zero;

            foreach (Blob blob in list) {
                blob.DynScale = Calc.LerpClamp(scale, textScale, blob.ScaleFactor);
                blob.DynY = sizeAll.Y;
                Vector2 size = CelesteNetClientFont.Measure(blob.Text) * blob.DynScale;
                sizeAll.X = Math.Max(sizeAll.X, size.X);
                sizeAll.Y += size.Y + 10f * scale;

                if (((sizeAll.X + 100f * scale) > UI_WIDTH * 0.7f || (sizeAll.Y + 90f * scale) > UI_HEIGHT * 0.7f) && textScaleTry < 5) {
                    textScaleTry++;
                    textScale -= scale * 0.1f;
                    goto RetryLineScale;
                }
            }

            Context.RenderHelper.Rect(25f * scale, y - 25f * scale, sizeAll.X + 50f * scale, sizeAll.Y + 40f * scale, Color.Black * 0.8f);

            foreach (Blob blob in list) {
                CelesteNetClientFont.Draw(
                    blob.Text,
                    new Vector2(50f * scale, y + blob.DynY),
                    Vector2.Zero,
                    new Vector2(blob.DynScale, blob.DynScale),
                    blob.Color
                );
            }

        }

        public class Blob {
            public string Text = "";
            public Color Color = Color.White;
            public float ScaleFactor = 0f;
            public float DynY;
            public float DynScale;
        }

    }
}
