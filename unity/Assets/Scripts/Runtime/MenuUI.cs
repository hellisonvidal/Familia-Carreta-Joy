using UnityEngine;

namespace CarretaJoy
{
    /// <summary>Menus: inicial, personagem, ponto de partida, ajustes, pausa e troca de personagem.</summary>
    public class MenuUI : MonoBehaviour
    {
        public enum Page { None, Main, Chars, Start, Settings, Pause, Picker }
        public Page page = Page.Main;
        Page back = Page.Main;
        public Game game;
        Texture2D rr, white;
        GUIStyle title, sub, btn, btnSel, lbl, small;
        float u;
        Vector2 scroll;

        void Awake()
        {
            rr = TouchUI.MakeRoundRect(64, 18);
            white = Texture2D.whiteTexture;
        }

        int openedFrame = -1;
        public void Open(Page p) { openedFrame = Time.frameCount; if (p == Page.Settings || p == Page.Chars || p == Page.Start) back = page; page = p; scroll = Vector2.zero; }

        void Styles()
        {
            u = Mathf.Min(Screen.width, Screen.height) / 100f;
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

        void OnGUI()
        {
            if (page == Page.None || game == null) return;
            Styles();
            GUI.depth = 0;
            float W = Screen.width, H = Screen.height;
            // fundo escurecido
            GUI.color = new Color(0.05f, 0.02f, 0.1f, page == Page.Main ? 0.35f : 0.6f);
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = Color.white;
            switch (page)
            {
                case Page.Main: Main(W, H); break;
                case Page.Chars: case Page.Picker: Chars(W, H); break;
                case Page.Start: StartPage(W, H); break;
                case Page.Settings: SettingsPage(W, H); break;
                case Page.Pause: Pause(W, H); break;
            }
        }

        bool B(Rect r, string s, bool sel = false) => GUI.Button(r, s, sel ? btnSel : btn);

        void Title(float W, float y, string t, string s)
        {
            var c = title.normal.textColor;
            title.normal.textColor = new Color(0, 0, 0, 0.5f); GUI.Label(new Rect(4, y + 4, W, 14 * u), t, title);
            title.normal.textColor = new Color(1f, 0.85f, 0.2f); GUI.Label(new Rect(0, y, W, 14 * u), t, title);
            title.normal.textColor = c;
            sub.normal.textColor = Color.white;
            if (s != null) GUI.Label(new Rect(0, y + 13 * u, W, 6 * u), s, sub);
        }

        void Main(float W, float H)
        {
            Title(W, 6 * u, "CARRETA JOY", "Ponta Grossa - PR");
            float bw = 62 * u, bh = 10 * u, x = (W - bw) / 2, y = 30 * u, gap = 12 * u;
            if (B(new Rect(x, y, bw, bh), "JOGAR", true)) game.StartGame();
            if (B(new Rect(x, y + gap, bw, bh), "Personagem: " + game.CharName(Settings.StartChar))) Open(Page.Chars);
            if (B(new Rect(x, y + gap * 2, bw, bh), "Partida: " + game.StopName(Settings.StartStop))) Open(Page.Start);
            if (B(new Rect(x, y + gap * 3, bw / 2 - u, bh), "Ajustes")) Open(Page.Settings);
            if (B(new Rect(x + bw / 2 + u, y + gap * 3, bw / 2 - u, bh), "Sair")) Application.Quit();
            small.alignment = TextAnchor.LowerCenter;
            GUI.Label(new Rect(0, H - 8 * u, W, 6 * u), (Pad.Connected ? "Controle: A jogar · Y personagem · X partida    " : "") + "Mapa: © colaboradores do OpenStreetMap", small);
            small.alignment = TextAnchor.MiddleLeft;
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
            float gx = 2 * u, cw = Mathf.Min(30 * u, (W - 8 * u - gx * (cols - 1)) / cols), ch = cw * 1.22f;
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
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) PickChar(i);
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
            if (page == Page.None || game == null || Time.frameCount == openedFrame) return;
            Pad.Update();
            bool a = Pad.Down(Pad.A), bk = Pad.Down(Pad.B), st = Pad.Start;
            int dx = (Pad.DRight || StickEdge(1) ? 1 : 0) - (Pad.DLeft || StickEdge(2) ? 1 : 0);
            int dy = (Pad.DDown || StickEdge(3) ? 1 : 0) - (Pad.DUp || StickEdge(4) ? 1 : 0);
            switch (page)
            {
                case Page.Main:
                    if (a || st) game.StartGame();
                    else if (Pad.Down(Pad.Y)) { cursor = Settings.StartChar; Open(Page.Chars); }
                    else if (Pad.Down(Pad.X)) Open(Page.Start);
                    break;
                case Page.Chars: case Page.Picker:
                    cursor = Mathf.Clamp(cursor + dx + dy * 5, 0, game.CharCount - 1);
                    if (a) PickChar(cursor);
                    else if (bk) page = page == Page.Picker ? Page.None : back;
                    break;
                case Page.Start:
                    cursor = Mathf.Clamp(cursor + dx + dy * 2, 0, game.StopCount - 1);
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
            Title(W, 2 * u, "PONTO DE PARTIDA", "Onde a carreta começa a volta pela cidade");
            int n = game.StopCount;
            float bw = 44 * u, bh = 9 * u, gx = 2 * u;
            float x0 = (W - (bw * 2 + gx)) / 2, y0 = 22 * u;
            for (int i = 0; i < n; i++)
            {
                var rect = new Rect(x0 + (i % 2) * (bw + gx), y0 + (i / 2) * (bh + gx), bw, bh);
                if (B(rect, game.StopName(i), Settings.StartStop == i || (Pad.Connected && cursor == i))) { Settings.StartStop = i; Settings.Save(); game.PreviewStart(); page = back; }
            }
            if (B(new Rect((W - bw) / 2, y0 + ((n + 1) / 2) * (bh + gx) + u, bw, bh), "Voltar")) page = back;
        }

        void SettingsPage(float W, float H)
        {
            Title(W, 2 * u, "AJUSTES", null);
            float x = W / 2 - 60 * u, y = 18 * u, rowH = 11 * u, lw = 40 * u, cw = 80 * u;
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
            Row("Sensibilidade"); Settings.Sensitivity = Mathf.Lerp(0.4f, 2f, Slider(new Rect(x + lw, y, cw, rowH * 0.85f), Mathf.InverseLerp(0.4f, 2f, Settings.Sensitivity))); y += rowH;
            if (B(new Rect(W / 2 - 20 * u, y + u, 40 * u, 10 * u), "OK", true)) { Settings.Save(); game.ApplySettings(); page = back; }
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
            float bw = 62 * u, bh = 10 * u, x = (W - bw) / 2, y = 22 * u, gap = 12 * u;
            if (B(new Rect(x, y, bw, bh), "Continuar", true)) { page = Page.None; game.Resume(); }
            if (B(new Rect(x, y + gap, bw, bh), "Trocar de personagem")) { cursor = game.PlayerIndex; Open(Page.Picker); }
            if (B(new Rect(x, y + gap * 2, bw / 2 - u, bh), "Ajustes")) Open(Page.Settings);
            if (B(new Rect(x + bw / 2 + u, y + gap * 2, bw / 2 - u, bh), game.TruckManual ? "Carreta: piloto auto" : "Recomeçar")) { if (game.TruckManual) game.TruckAuto(); else game.StartGame(); page = Page.None; game.Resume(); }
            if (B(new Rect(x, y + gap * 3, bw, bh), "Menu principal")) game.GoMenu();
        }
    }
}
