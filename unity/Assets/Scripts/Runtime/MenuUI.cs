using UnityEngine;

namespace CarretaJoy
{
    /// <summary>Menus: inicial, personagem, ponto de partida, ajustes, pausa e troca de personagem.</summary>
    public class MenuUI : MonoBehaviour
    {
        public enum Page { None, Main, Chars, Start, Settings, Pause, Picker, Trucks, Combos }
        public Page page = Page.Main;
        Page back = Page.Main;
        public Game game;
        Texture2D rr, white, grad, glow;
        GUIStyle title, sub, btn, btnSel, lbl, small;
        float u;
        Vector2 scroll;

        void Awake()
        {
            rr = TouchUI.MakeRoundRect(64, 18);
            white = Texture2D.whiteTexture;
            // fundo em degradê (roxo da noite até o rosa do neon) e brilho suave
            grad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) { float t = i / 63f; grad.SetPixel(0, i, Color.Lerp(new Color(0.35f, 0.05f, 0.35f, 0.85f), new Color(0.04f, 0.02f, 0.12f, 0.92f), t)); }
            grad.Apply();
            glow = TouchUI.MakeCircle(64);
            unlockTime = Time.unscaledTime + 0.8f;
        }

        int openedFrame = -1;
        float unlockTime;
        bool CanClick => Time.frameCount != openedFrame && Time.unscaledTime >= unlockTime;
        public void Open(Page p)
        {
            openedFrame = Time.frameCount;
            unlockTime = Time.unscaledTime + 0.35f;
            if (p == Page.Settings || p == Page.Chars || p == Page.Start || p == Page.Trucks || p == Page.Combos) back = page;
            page = p;
            scroll = Vector2.zero;
        }

        void Styles()
        {
            var sa = SafeRect(); u = Mathf.Min(sa.width, sa.height) / 100f;
            if (btn == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                sub = new GUIStyle(title) { fontStyle = FontStyle.Normal };
                lbl = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                small = new GUIStyle(lbl) { fontStyle = FontStyle.Normal, wordWrap = true };
                btn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(18, 18, 18, 18) };
                btn.normal.background = btn.hover.background = Tex(new Color(0.22f, 0.12f, 0.38f, 0.92f));
                btn.active.background = Tex(new Color(0.95f, 0.25f, 0.6f, 1f));
                btn.normal.textColor = btn.hover.textColor = btn.active.textColor = Color.white;
                btnSel = new GUIStyle(btn);
                btnSel.normal.background = btnSel.hover.background = Tex(new Color(0.95f, 0.45f, 0.1f, 0.95f));
            }
            title.fontSize = Mathf.RoundToInt(12 * u); sub.fontSize = Mathf.RoundToInt(3.6f * u);
            btn.fontSize = btnSel.fontSize = Mathf.RoundToInt(4 * u);
            lbl.fontSize = Mathf.RoundToInt(3.6f * u); small.fontSize = Mathf.RoundToInt(2.8f * u);
        }

        Texture2D Tex(Color c)
        {
            // botão arredondado colorido
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = rr.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(c.r, c.g, c.b, c.a * px[i].a);
            t.SetPixels(px); t.Apply();
            return t;
        }

        /// <summary>Área segura da tela (sem entalhe/câmera/cantos arredondados), em coordenadas da GUI.</summary>
        static Rect SafeRect()
        {
            var sa = Screen.safeArea;
            if (sa.width < 100 || sa.height < 100) return new Rect(0, 0, Screen.width, Screen.height);
            return new Rect(sa.x, Screen.height - sa.yMax, sa.width, sa.height);
        }

        void OnGUI()
        {
            if (page == Page.None || game == null) return;
            Styles();
            GUI.depth = 0;
            float W = Screen.width, H = Screen.height;
            // fundo escurecido
            if (page == Page.Main) { GUI.color = new Color(1, 1, 1, 0.55f); GUI.DrawTexture(new Rect(0, 0, W * 0.5f, H), grad); }
            else { GUI.color = Color.white; GUI.DrawTexture(new Rect(0, 0, W, H), grad); }
            // luzes de festa ao fundo
            float tt = Time.unscaledTime;
            for (int i = 0; i < 5; i++)
            {
                GUI.color = Color.HSVToRGB(Mathf.Repeat(tt * 0.05f + i * 0.2f, 1f), 0.8f, 1f) * new Color(1, 1, 1, 0.10f);
                float gx = W * (0.1f + 0.2f * i) + Mathf.Sin(tt * 0.6f + i) * 40 * u, gs = 50 * u;
                GUI.DrawTexture(new Rect(gx - gs / 2, H * 0.08f - gs / 2, gs, gs), glow);
            }
            GUI.color = Color.white;
            // o conteúdo fica todo dentro da área segura
            var g = SafeRect();
            GUI.BeginGroup(g);
            W = g.width; H = g.height;
            switch (page)
            {
                case Page.Main: Main(W, H); break;
                case Page.Chars: case Page.Picker: Chars(W, H); break;
                case Page.Start: StartPage(W, H); break;
                case Page.Settings: SettingsPage(W, H); break;
                case Page.Pause: Pause(W, H); break;
                case Page.Trucks: Trucks(W, H); break;
                case Page.Combos: CombosPage(W, H); break;
            }
            GUI.EndGroup();
        }

        bool B(Rect r, string s, bool sel = false) => GUI.Button(r, s, sel ? btnSel : btn) && CanClick;

        /// <summary>Aviso de versão nova no menu inicial: baixa e instala sem sair do jogo.</summary>
        void UpdateBox(float W, float x, float y, float w)
        {
            var up = game.updater;
            if (up == null || w < 20 * u || !(up.Available || up.Busy && up.Progress > 0)) return;
            var r = new Rect(x, y, w, 8 * u);
            if (up.Busy)
            {
                GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(r, rr); GUI.color = new Color(0.3f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(up.Progress), r.height), rr); GUI.color = Color.white;
                lbl.alignment = TextAnchor.MiddleCenter; GUI.Label(r, up.Status, small); lbl.alignment = TextAnchor.MiddleLeft;
            }
            else if (B(r, "ATUALIZAR PARA " + up.Latest, true)) up.DownloadAndInstall();
            if (!up.Busy) GUI.Label(new Rect(x, r.yMax, w, 5 * u), up.Status, small);
        }

        void Title(float W, float y, string t, string s)
        {
            var c = title.normal.textColor;
            title.normal.textColor = new Color(0, 0, 0, 0.5f); GUI.Label(new Rect(4, y + 4, W, 14 * u), t, title);
            title.normal.textColor = new Color(1f, 0.85f, 0.2f); GUI.Label(new Rect(0, y, W, 14 * u), t, title);
            title.normal.textColor = c;
            sub.normal.textColor = Color.white;
            if (s != null) GUI.Label(new Rect(0, y + 13 * u, W, 6 * u), s, sub);
        }

        readonly System.Collections.Generic.Dictionary<string, Texture2D> previews = new System.Collections.Generic.Dictionary<string, Texture2D>();
        Texture2D Preview(string name)
        {
            if (!previews.TryGetValue(name, out var t)) previews[name] = t = Resources.Load<Texture2D>("Previews/" + name);
            return t;
        }

        /// <summary>Cartão com foto, moldura neon e legenda.</summary>
        bool Card(Rect r, Texture img, string caption, bool sel, bool hover = false, string note = null, bool clickable = true)
        {
            // sombra
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(r.x + u * 0.8f, r.y + u * 1.0f, r.width, r.height), rr, ScaleMode.StretchToFill, true, 0, GUI.color, 0, 2.2f * u);
            // moldura
            Color frame = sel ? new Color(1f, 0.55f, 0.1f) : hover ? new Color(0.95f, 0.25f, 0.6f) : new Color(0.3f, 0.18f, 0.5f, 0.95f);
            if (sel) frame = Color.Lerp(frame, new Color(1f, 0.85f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
            GUI.DrawTexture(r, rr, ScaleMode.StretchToFill, true, 0, frame, 0, 2.2f * u);
            var inner = new Rect(r.x + 0.7f * u, r.y + 0.7f * u, r.width - 1.4f * u, r.height - 1.4f * u);
            GUI.DrawTexture(inner, rr, ScaleMode.StretchToFill, true, 0, new Color(0.08f, 0.04f, 0.14f, 1f), 0, 1.8f * u);
            float capH = Mathf.Max(5.5f * u, r.height * 0.2f);
            var imgR = new Rect(inner.x, inner.y, inner.width, inner.height - capH);
            GUI.color = Color.white;
            if (img != null) GUI.DrawTexture(imgR, img, ScaleMode.ScaleAndCrop, true, 0, Color.white, 0, 1.8f * u);
            lbl.alignment = TextAnchor.MiddleCenter;
            int fs = lbl.fontSize;
            lbl.fontSize = Mathf.RoundToInt(Mathf.Min(3.4f * u, r.width / Mathf.Max(8, caption.Length) * 1.7f));
            GUI.Label(new Rect(inner.x, imgR.yMax, inner.width, capH), caption, lbl);
            lbl.fontSize = fs; lbl.alignment = TextAnchor.MiddleLeft;
            if (note != null) { GUI.color = new Color(1, 0.85f, 0.2f); GUI.Label(new Rect(imgR.x + u, imgR.y + u, imgR.width, 4 * u), note, small); GUI.color = Color.white; }
            return clickable && GUI.Button(r, GUIContent.none, GUIStyle.none) && CanClick;
        }

        void Main(float W, float H)
        {
            // coluna da esquerda: botões; direita: vitrine visual da carreta, personagem e local escolhidos
            float x = 5 * u, bw = Mathf.Min(58 * u, W * 0.36f), bh = 8f * u, gap = 9.4f * u, y = 26 * u;
            var c = title.alignment; title.alignment = TextAnchor.MiddleLeft; sub.alignment = TextAnchor.MiddleLeft;
            title.normal.textColor = new Color(0, 0, 0, 0.5f); GUI.Label(new Rect(x + 4, 4 * u + 4, W, 14 * u), "CARRETA JOY", title);
            title.normal.textColor = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.08f, 1f), 0.35f, 1f); GUI.Label(new Rect(x, 4 * u, W, 14 * u), "CARRETA JOY", title);
            sub.normal.textColor = Color.white; GUI.Label(new Rect(x + u, 17 * u, W, 6 * u), "Ponta Grossa - PR", sub);
            title.alignment = c; sub.alignment = TextAnchor.MiddleCenter;
            if (B(new Rect(x, y, bw, bh * 1.15f), "JOGAR", true)) game.StartGame();
            y += gap * 1.12f;
            if (B(new Rect(x, y, bw, bh), "Personagem: " + game.CharName(Settings.StartChar))) Open(Page.Chars); y += gap;
            if (B(new Rect(x, y, bw, bh), "Carreta: " + game.TruckName(Settings.Truck))) Open(Page.Trucks); y += gap;
            if (B(new Rect(x, y, bw, bh), "Partida: " + game.StopName(Settings.StartStop))) Open(Page.Start); y += gap;
            if (B(new Rect(x, y, bw / 2 - u, bh), "Combos")) Open(Page.Combos);
            if (B(new Rect(x + bw / 2 + u, y, bw / 2 - u, bh), "Ajustes")) Open(Page.Settings); y += gap;
            if (B(new Rect(x, y, bw, bh * 0.8f), "Sair")) Application.Quit();

            // vitrine apenas visual (evita abrir a seleção de carreta por toque acidental ao abrir o jogo)
            float sx = x + bw + 5 * u, sw = W - sx - 4 * u;
            if (sw > 40 * u)
            {
                float cw = Mathf.Min(sw, (H - 30 * u) * 1.6f), chh = cw / 1.6f;
                var big = new Rect(sx + (sw - cw) / 2, 6 * u, cw, chh);
                Card(big, Preview(game.TruckFile(Settings.Truck)), game.TruckName(Settings.Truck), false, false, null, false);
                float sw2 = (cw - 2 * u) / 2, sh2 = Mathf.Min(H - big.yMax - 14 * u, sw2 / 1.6f);
                if (sh2 > 12 * u)
                {
                    sw2 = Mathf.Min(sw2, sh2 * 1.6f);
                    Card(new Rect(big.x, big.yMax + 2 * u, sw2, sh2), Preview("Partida_" + Settings.StartStop), game.StopName(Settings.StartStop), false, false, null, false);
                    Card(new Rect(big.xMax - sh2 * 0.85f, big.yMax + 2 * u, sh2 * 0.85f, sh2), Portrait(Settings.StartChar), game.CharName(Settings.StartChar), false, false, null, false);
                }
            }
            UpdateBox(W, x, H - 15 * u, bw);
            small.alignment = TextAnchor.LowerRight;
            GUI.Label(new Rect(0, H - 10 * u, W - 3 * u, 4.5f * u), "versão " + Application.version, small);
            GUI.Label(new Rect(0, H - 5.5f * u, W - 3 * u, 5 * u), (Pad.Connected ? "Controle: A jogar · Y personagem · X partida · LB carreta    " : "") + "Mapa: © colaboradores do OpenStreetMap", small);
            small.alignment = TextAnchor.MiddleLeft;
        }

        void Trucks(float W, float H)
        {
            Title(W, 1 * u, "ESCOLHA A CARRETA", null);
            int n = game.TruckCount;
            float gx = 3 * u, cw = Mathf.Min((W - 12 * u - gx) / 2, (H - 36 * u) / 2 * 1.6f), ch = cw / 1.6f;
            float x0 = (W - (cw * 2 + gx)) / 2, y0 = 15 * u;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + (i % 2) * (cw + gx), y0 + (i / 2) * (ch + gx), cw, ch);
                if (Card(r, Preview(game.TruckFile(i)), game.TruckName(i), Settings.Truck == i, Pad.Connected && cursor == i)) { PickTruck(i); }
            }
            float bw = 40 * u, bh = 8 * u;
            if (B(new Rect((W - bw) / 2, y0 + 2 * (ch + gx), bw, bh), "Voltar")) page = back;
        }

        void PickTruck(int i) { Settings.Truck = i; Settings.Save(); game.PreviewStart(); page = back; }

        void CombosPage(float W, float H)
        {
            Title(W, 1 * u, "COMBOS", "Aperte os dois botões em seguida (menos de 1 segundo)");
            float x = W / 2 - 62 * u, y = 21 * u, rowH = 6.4f * u, colW = 62 * u;
            void Line(float cx, float cy, string a, string b)
            {
                GUI.DrawTexture(new Rect(cx, cy, colW - 2 * u, rowH - 0.8f * u), rr, ScaleMode.StretchToFill, true, 0, new Color(0.25f, 0.12f, 0.4f, 0.85f), 0, 1.4f * u);
                GUI.Label(new Rect(cx + 2 * u, cy, colW * 0.55f, rowH - 0.8f * u), a, lbl);
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUI.Label(new Rect(cx + colW * 0.55f, cy, colW * 0.45f - 3 * u, rowH - 0.8f * u), b, lbl);
                GUI.color = Color.white;
            }
            int fs = lbl.fontSize; lbl.fontSize = Mathf.RoundToInt(2.9f * u);
            GUI.Label(new Rect(x, y - 5 * u, colW, 5 * u), "ACROBACIAS", lbl);
            float yy = y;
            foreach (var c in Game.Combos) { Line(x, yy, Game.BtnName(c.a) + " + " + Game.BtnName(c.b), Kid.TrickTitle(c.move)); yy += rowH; }
            GUI.Label(new Rect(x + colW + 2 * u, y - 5 * u, colW, 5 * u), "DANÇANDO + BOTÃO", lbl);
            yy = y;
            foreach (var d in Game.DanceCombos)
            {
                Line(x + colW + 2 * u, yy, "DANÇAR + " + Game.BtnName(d.btn), d.move != null ? Kid.TrickTitle(d.move) : Kid.DanceNames[d.style]);
                yy += rowH;
            }
            Line(x + colW + 2 * u, yy, "DANÇAR de novo", "troca a dança"); yy += rowH;
            lbl.fontSize = fs;
            GUI.Label(new Rect(x, Mathf.Max(yy, y + rowH * Game.Combos.Length) + u, colW * 2, 6 * u), "Manobras emendadas (uma logo depois da outra) contam SEQUÊNCIA; 5 seguidas = SUPER COMBO!", small);
            float bw = 40 * u, bh = 8 * u;
            if (B(new Rect((W - bw) / 2, H - bh - 3 * u, bw, bh), "Voltar")) page = back;
        }

        readonly System.Collections.Generic.Dictionary<int, Texture2D> portraits = new System.Collections.Generic.Dictionary<int, Texture2D>();
        Texture2D Portrait(int i)
        {
            if (!portraits.TryGetValue(i, out var t)) portraits[i] = t = Resources.Load<Texture2D>("Portraits/" + game.CharId(i));
            return t;
        }
        int cursor;   // seleção pelo controle de videogame

        void Chars(float W, float H)
        {
            bool inGame = page == Page.Picker;
            Title(W, 1 * u, inGame ? "QUEM VAI JOGAR?" : "ESCOLHA O PERSONAGEM", null);
            int n = game.CharCount, cols = 5;
            float gx = 2 * u, cw = Mathf.Min(30 * u, (W - 8 * u - gx * (cols - 1)) / cols, (H - 30 * u - gx * 2) / (2 * 1.22f)), ch = cw * 1.22f;
            float x0 = (W - (cw * cols + gx * (cols - 1))) / 2, y0 = 15 * u;
            for (int i = 0; i < n; i++)
            {
                int c = i % cols, r = i / cols;
                var rect = new Rect(x0 + c * (cw + gx), y0 + r * (ch + gx), cw, ch);
                bool sel = inGame ? game.PlayerIndex == i && !game.Driving : Settings.StartChar == i;
                bool hover = Pad.Connected && cursor == i;
                // cartão com a foto
                GUI.color = sel ? new Color(0.95f, 0.45f, 0.1f) : hover ? new Color(0.95f, 0.25f, 0.6f) : new Color(0.22f, 0.12f, 0.38f, 0.95f);
                GUI.DrawTexture(rect, rr, ScaleMode.StretchToFill, true, 0, GUI.color, 0, 2.5f * u);
                GUI.color = Color.white;
                var pr = Portrait(i);
                var img = new Rect(rect.x + u, rect.y + u, rect.width - 2 * u, rect.width - 2 * u);
                if (pr != null) GUI.DrawTexture(img, pr, ScaleMode.ScaleToFit, true);
                lbl.alignment = TextAnchor.MiddleCenter; lbl.fontSize = Mathf.RoundToInt(Mathf.Min(3.4f * u, cw / Mathf.Max(6, game.CharName(i).Length) * 1.7f));
                GUI.Label(new Rect(rect.x, img.yMax, rect.width, rect.yMax - img.yMax), game.CharName(i), lbl);
                lbl.alignment = TextAnchor.MiddleLeft;
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && CanClick) PickChar(i);
            }
            float bw = 46 * u, bh = 9 * u, y = y0 + 2 * (ch + gx) + u;
            if (inGame)
            {
                if (B(new Rect(W / 2 - bw - u, y, bw, bh), game.Driving ? "SAIR DA CARRETA" : "DIRIGIR A CARRETA", true)) { game.ToggleDrive(true); page = Page.None; }
                if (B(new Rect(W / 2 + u, y, bw, bh), "Voltar")) page = Page.None;
            }
            else if (B(new Rect((W - bw) / 2, y, bw, bh), "Voltar")) page = back;
        }

        void PickChar(int i)
        {
            if (page == Page.Picker) { game.SelectPlayer(i); page = Page.None; }
            else { Settings.StartChar = i; Settings.Save(); page = back; }
        }

        // ---------------- controle de videogame nos menus ----------------
        void Update()
        {
            if (page == Page.None || game == null || !CanClick) return;
            Pad.Update();
            if (!Pad.Connected) return;
            bool a = Pad.Down(Pad.A), bk = Pad.Down(Pad.B), st = Pad.Start;
            int dx = (Pad.DRight || StickEdge(1) ? 1 : 0) - (Pad.DLeft || StickEdge(2) ? 1 : 0);
            int dy = (Pad.DDown || StickEdge(3) ? 1 : 0) - (Pad.DUp || StickEdge(4) ? 1 : 0);
            switch (page)
            {
                case Page.Main:
                    if (a || st) game.StartGame();
                    else if (Pad.Down(Pad.Y)) { cursor = Settings.StartChar; Open(Page.Chars); }
                    else if (Pad.Down(Pad.X)) { cursor = Settings.StartStop; Open(Page.Start); }
                    else if (Pad.Down(Pad.LB)) { cursor = Settings.Truck; Open(Page.Trucks); }
                    break;
                case Page.Trucks:
                    cursor = Mathf.Clamp(cursor + dx + dy * 2, 0, game.TruckCount - 1);
                    if (a) PickTruck(cursor);
                    else if (bk) page = back;
                    break;
                case Page.Combos:
                    if (a || bk || st) page = back;
                    break;
                case Page.Chars: case Page.Picker:
                    cursor = Mathf.Clamp(cursor + dx + dy * 5, 0, game.CharCount - 1);
                    if (a) PickChar(cursor);
                    else if (bk) page = page == Page.Picker ? Page.None : back;
                    break;
                case Page.Start:
                    cursor = Mathf.Clamp(cursor + dx + dy * 5, 0, game.StopCount - 1);
                    if (a) { Settings.StartStop = cursor; Settings.Save(); game.PreviewStart(); page = back; }
                    else if (bk) page = back;
                    break;
                case Page.Settings:
                    if (bk || a || st) { Settings.Save(); game.ApplySettings(); page = back; }
                    break;
                case Page.Pause:
                    if (a || bk || st) page = Page.None;
                    else if (Pad.Down(Pad.Y)) { cursor = game.PlayerIndex; Open(Page.Picker); }
                    break;
            }
        }

        float stickT;
        bool StickEdge(int dir)
        {
            // empurrar o analógico também move a seleção (com intervalo)
            var v = Pad.Left; bool on = dir == 1 ? v.x > 0.6f : dir == 2 ? v.x < -0.6f : dir == 3 ? v.y < -0.6f : v.y > 0.6f;
            if (!on || Time.unscaledTime < stickT) return false;
            stickT = Time.unscaledTime + 0.25f; return true;
        }

        void StartPage(float W, float H)
        {
            Title(W, 1 * u, "PONTO DE PARTIDA", null);
            int n = game.StopCount, cols = 5;
            float gx = 2 * u, cw = Mathf.Min((W - 8 * u - gx * (cols - 1)) / cols, ((H - 34 * u) / 2) * 1.45f), ch = cw / 1.45f;
            float x0 = (W - (cw * cols + gx * (cols - 1))) / 2, y0 = 15 * u;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + (i % cols) * (cw + gx), y0 + (i / cols) * (ch + gx), cw, ch);
                if (Card(r, Preview("Partida_" + i), game.StopName(i), Settings.StartStop == i, Pad.Connected && cursor == i))
                { Settings.StartStop = i; Settings.Save(); game.PreviewStart(); page = back; }
            }
            float bw = 40 * u, bh = 8 * u;
            if (B(new Rect((W - bw) / 2, y0 + ((n + cols - 1) / cols) * (ch + gx) + u, bw, bh), "Voltar")) page = back;
        }

        void SettingsPage(float W, float H)
        {
            Title(W, 2 * u, "AJUSTES", null);
            float x = W / 2 - 60 * u, y = 15 * u, rowH = Mathf.Min(9.4f * u, (H - 15 * u - 24 * u) / 8f), lw = 40 * u, cw = 80 * u;
            void Row(string name) { GUI.Label(new Rect(x, y, lw, rowH * 0.9f), name, lbl); }
            int Choice(int cur, string[] opts)
            {
                float w = cw / opts.Length;
                for (int i = 0; i < opts.Length; i++)
                    if (B(new Rect(x + lw + i * w, y, w - u, rowH * 0.85f), opts[i], cur == i)) cur = i;
                return cur;
            }
            Row("Música"); Settings.Music = Slider(new Rect(x + lw, y, cw, rowH * 0.85f), Settings.Music); y += rowH;
            Row("Gráficos"); Settings.Quality = Choice(Settings.Quality, Settings.QualityNames); y += rowH;
            Row("Quadros/s"); Settings.Fps = Choice(Settings.Fps == 30 ? 0 : 1, new[] { "30 (economia)", "60 (suave)" }) == 0 ? 30 : 60; y += rowH;
            Row("Câmera"); Settings.CamMode = Choice(Settings.CamMode, Settings.CamNames); y += rowH;
            Row("Turma"); Settings.CrewMode = Choice(Settings.CrewMode, Settings.CrewNames); y += rowH;
            Row("Carreta"); Settings.TruckSpeed = Choice(Settings.TruckSpeed, Settings.TruckSpeedNames); y += rowH;
            Row("Dirigir"); Settings.SafeDrive = Choice(Settings.SafeDrive ? 0 : 1, new[] { "Modo seguro (devagar)", "Livre" }) == 0; y += rowH;
            Row("Sensibilidade"); Settings.Sensitivity = Mathf.Lerp(0.4f, 2f, Slider(new Rect(x + lw, y, cw, rowH * 0.85f), Mathf.InverseLerp(0.4f, 2f, Settings.Sensitivity))); y += rowH;
            if (B(new Rect(W / 2 - 20 * u, y + u, 40 * u, 9 * u), "OK", true)) { Settings.Save(); game.ApplySettings(); page = back; }
            // atualizações
            var up = game.updater;
            if (up != null)
            {
                float ux = W / 2 + 24 * u, uy = y + u;
                if (!up.Busy && B(new Rect(ux, uy, 36 * u, 9 * u), up.Available ? "ATUALIZAR " + up.Latest : "Procurar versão nova", up.Available))
                { if (up.Available) up.DownloadAndInstall(); else up.Check(); }
                GUI.Label(new Rect(W / 2 - 60 * u, uy + 9.5f * u, 120 * u, 4.5f * u), "versão " + Application.version + "   " + up.Status, small);
            }
        }

        float Slider(Rect r, float v)
        {
            // barra grande, fácil de usar com o dedo
            GUI.color = new Color(1, 1, 1, 0.25f); GUI.DrawTexture(new Rect(r.x, r.center.y - u, r.width, 2 * u), white);
            GUI.color = new Color(0.95f, 0.45f, 0.1f); GUI.DrawTexture(new Rect(r.x, r.center.y - u, r.width * v, 2 * u), white);
            GUI.color = Color.white;
            float kx = r.x + r.width * v; GUI.DrawTexture(new Rect(kx - 3 * u, r.center.y - 3 * u, 6 * u, 6 * u), rr);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && new Rect(r.x - 3 * u, r.y, r.width + 6 * u, r.height).Contains(e.mousePosition))
            { v = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width); e.Use(); }
            return v;
        }

        void Pause(float W, float H)
        {
            Title(W, 4 * u, "PAUSA", null);
            float bw = 62 * u, bh = 9 * u, x = (W - bw) / 2, y = 20 * u, gap = 11f * u;
            if (B(new Rect(x, y, bw, bh), "Continuar", true)) { page = Page.None; game.Resume(); }
            if (B(new Rect(x, y + gap * 4, bw, bh), "Próxima música" + (game.MusicTitle != "" ? " (agora: " + game.MusicTitle + ")" : ""))) game.NextMusic();
            if (B(new Rect(x, y + gap, bw, bh), "Trocar de personagem")) { cursor = game.PlayerIndex; Open(Page.Picker); }
            if (B(new Rect(x, y + gap * 2, bw / 2 - u, bh), "Ajustes")) Open(Page.Settings);
            if (B(new Rect(x + bw / 2 + u, y + gap * 2, bw / 2 - u, bh), game.TruckManual ? "Carreta: piloto auto" : "Recomeçar")) { if (game.TruckManual) game.TruckAuto(); else game.StartGame(); page = Page.None; game.Resume(); }
            if (B(new Rect(x, y + gap * 3, bw / 2 - u, bh), "Combos")) Open(Page.Combos);
            if (B(new Rect(x + bw / 2 + u, y + gap * 3, bw / 2 - u, bh), "Menu principal")) game.GoMenu();
        }
    }
}
