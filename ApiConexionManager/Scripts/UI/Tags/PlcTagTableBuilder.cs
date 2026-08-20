//----------------------------------------------------------------------------------------------------------------------
// PLC TAG TABLE BUILDER
//
// Desc: Factoría estática que construye y devuelve un ListView configurado para mostrar
//       tags PLC. Responsabilidad única: crear y enlazar elementos visuales.
//       No sabe nada de ApiInterface ni de PlcTagDataService.
//
// Uso:  var list = PlcTagTableBuilder.Build(rows, onWriteRequested);
//       container.Add(PlcTagTableBuilder.BuildHeader());
//       PlcTagTableBuilder.SetFontSize(12f);   // escala toda la tabla
//
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public static class PlcTagTableBuilder
{
    // -- Paleta -----------------------------------------------------------------

    private static readonly Color ColBg       = new Color(0.08f, 0.09f, 0.10f, 1f);
    private static readonly Color ColRowEven  = new Color(0.11f, 0.12f, 0.13f, 1f);
    private static readonly Color ColRowOdd   = new Color(0.09f, 0.10f, 0.11f, 1f);
    private static readonly Color ColAccent   = new Color(0.25f, 0.85f, 0.55f, 1f);
    private static readonly Color ColText     = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ColMuted    = new Color(0.50f, 0.52f, 0.54f, 1f);
    private static readonly Color ColBtnTrue  = new Color(0.10f, 0.60f, 0.25f, 1f);
    private static readonly Color ColBtnFalse = new Color(0.62f, 0.12f, 0.12f, 1f);
    private static readonly Color ColBtnOk    = new Color(0.15f, 0.40f, 0.75f, 1f);
    private static readonly Color ColTfBorder = new Color(0.30f, 0.32f, 0.35f, 1f);
    private static readonly Color ColTfBg     = new Color(0.18f, 0.19f, 0.21f, 1f);

    // -- Fuente escalable -------------------------------------------------------

    private static float _fontSize = 11f;

    /// <summary>
    /// Actualiza el tamaño de fuente global de la tabla.
    /// Requiere llamar a ListView.Rebuild() a posteriori para aplicarse.
    /// </summary>
    public static void SetFontSize(float size) => _fontSize = Mathf.Clamp(size, 9f, 16f);

    // -- Anchos de columna ------------------------------------------------------

    private const float PWMenu = 0.05f;
    private const float PWName = 0.22f; 
    private const float PWType = 0.18f;
    private const float PWVal  = 0.14f;
    private const float PWArea = 0.06f;
    private const float PWMod  = 0.35f; 

    private static float _panelWidth = 640f;
    private static float _pwNameDynamic = PWName;
    private static float _pwValDynamic  = PWVal;

    public static void SetPanelWidth(float w) => _panelWidth = Mathf.Max(w, 100f);

    /// <summary>
    /// Ajusta las proporciones de las columnas en base a la longitud de los nombres para evitar cortes.
    /// </summary>
    public static void AdjustColumnWidths(List<RowData> items)
    {
        if (items == null || items.Count == 0) return;

        int maxChars = 0;
        foreach (var row in items)
            if (row.Name != null && row.Name.Length > maxChars)
                maxChars = row.Name.Length;

        float charWidth = _fontSize * 0.65f;
        float neededWidth = maxChars * charWidth + 16f;
        float neededProportion = neededWidth / _panelWidth;

        // 👇 CAMBIO AQUÍ: Cambia el máximo de 0.45f a 0.28f para evitar el overflow
        _pwNameDynamic = Mathf.Clamp(neededProportion, 0.18f, 0.28f); 
        float remaining = 1f - _pwNameDynamic - PWMenu - PWType - PWArea - PWMod;
        _pwValDynamic = Mathf.Max(0.08f, remaining);
    }

    private static float W(float proportion) => Mathf.Floor(_panelWidth * proportion);

    /// <summary>
    /// Actualiza el tamaño de las celdas de cabecera tras un reajuste de anchos.
    /// </summary>
    public static void RefreshHeader(VisualElement header)
    {
        if (header == null) { Debug.Log("HEADER ES NULL"); return; }
        var cells = header.Children().ToList();
        Debug.Log($"Celdas en header: {cells.Count} | _pwNameDynamic: {_pwNameDynamic} | W: {W(_pwNameDynamic)}");
        if (cells.Count < 6) return;
        
        cells[0].style.width = W(PWMenu); 
        cells[1].style.width = W(_pwNameDynamic);
        cells[2].style.width = W(PWType);
        cells[3].style.width = W(_pwValDynamic);
        cells[4].style.width = W(PWArea);
        cells[5].style.width = W(PWMod);
    }

    // -- Modelo de fila ---------------------------------------------------------

    public class RowData
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;
        public string EditBuffer;

        public bool   IsGroupHeader; 
        public string GroupLabel;    
        public bool   IsCollapsed;   
    }

    // -- API pública ------------------------------------------------------------

    /// <summary>
    /// Construye y retorna la barra de encabezados de la tabla.
    /// </summary>
    public static VisualElement BuildHeader()
    {
        var row = MakeRow(ColBg);
        row.name = "plc-header";
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = ColAccent;
        row.style.paddingBottom     = 4f;

        row.Add(HeaderCell("", PWMenu));
        row.Add(HeaderCell("Nombre",    _pwNameDynamic));
        row.Add(HeaderCell("Tipo",      PWType));
        row.Add(HeaderCell("Valor",     _pwValDynamic));
        row.Add(HeaderCell("E/S",       PWArea));
        row.Add(HeaderCell("Modificar", PWMod));

        return row;
    }

    /// <summary>
    /// Construye el ListView inyectándole las lógicas de dibujo e interacción (callbacks).
    /// </summary>
    public static ListView Build(
        List<RowData>          items,
        Action<string, string> onWriteRequested,
        Action<string, VisualElement> onMenuClick)
    {
        float itemHeight = Mathf.Max(GroupHeaderHeight, Mathf.Max(24f, _fontSize * 2.4f));

        var lv = new ListView(
            items,
            itemHeight,
            MakeItem,
            (el, i) => BindItem(el, i, items, onWriteRequested, onMenuClick))
        {
            selectionType = SelectionType.None,
            showAlternatingRowBackgrounds = AlternatingRowBackground.None,
            style =
            {
                backgroundColor = ColBg,
                // Flexbox completo para forzar la barra de scroll cuando es necesario
                flexGrow        = 1f, 
                flexShrink      = 1f,
                minHeight       = 0f, 
            }
        };

        return lv;
    }

    // -- MakeItem / BindItem ----------------------------------------------------

    private const float GroupHeaderHeight = 26f;

    /// <summary>
    /// Crea la estructura del contenedor visual base que reutilizará el ListView.
    /// </summary>
    private static VisualElement MakeItem()
    {
        var row = new VisualElement { name = "plc-row" };
        row.style.flexDirection = FlexDirection.Column;

        // Estructura Cabecera de grupo
        var groupHeader = new VisualElement { name = "group-header" };
        groupHeader.style.flexDirection   = FlexDirection.Row;
        groupHeader.style.alignItems      = Align.Center;
        groupHeader.style.paddingLeft     = 8f;
        groupHeader.style.paddingRight    = 8f;
        groupHeader.style.height          = GroupHeaderHeight;
        groupHeader.style.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 1f);
        groupHeader.style.borderTopColor  = ColAccent;
        groupHeader.style.borderTopWidth  = 1f;
        groupHeader.style.display         = DisplayStyle.None;

        var chevron = new Label("▼") { name = "group-chevron" };
        chevron.style.color     = ColAccent;
        chevron.style.fontSize  = 10f;
        chevron.style.width     = 18f;
        chevron.style.unityTextAlign = TextAnchor.MiddleCenter;
        groupHeader.Add(chevron);

        var groupLbl = new Label("") { name = "group-label" };
        groupLbl.style.color                   = ColAccent;
        groupLbl.style.fontSize                = _fontSize;
        groupLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        groupLbl.style.flexGrow                = 1f;
        groupHeader.Add(groupLbl);

        var countLbl = new Label("") { name = "group-count" };
        countLbl.style.color    = ColMuted;
        countLbl.style.fontSize = _fontSize - 1f;
        groupHeader.Add(countLbl);

        row.Add(groupHeader);

        // Estructura Fila de datos
        var dataRow = MakeDataRow();
        dataRow.name = "data-row";
        row.Add(dataRow);

        return row;
    }

    private static VisualElement MakeDataRow()
    {
        var row = MakeRow(Color.clear);
        row.name = "plc-data-row";

        // Celda Menú
        var menuCell = new VisualElement { name = "cell-menu" };
        menuCell.style.width = W(PWMenu); menuCell.style.flexShrink = 0f; menuCell.style.alignItems = Align.Center; menuCell.style.justifyContent = Justify.Center;
        var btnMenu = new Button { name = "btn-menu", text = "⋮" };
        btnMenu.style.width = 18f; btnMenu.style.height = 18f; btnMenu.style.paddingLeft = 0f; btnMenu.style.paddingRight = 0f; btnMenu.style.paddingTop = 0f; btnMenu.style.paddingBottom = 0f; btnMenu.style.fontSize = 13f;
        btnMenu.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f); btnMenu.style.color = ColText; btnMenu.style.borderTopWidth = 0f; btnMenu.style.borderBottomWidth = 0f; btnMenu.style.borderLeftWidth = 0f; btnMenu.style.borderRightWidth = 0f;
        menuCell.Add(btnMenu);
        row.Add(menuCell);

        row.Add(DataCell("", _pwNameDynamic, "cell-name"));
        row.Add(DataCell("", PWType, "cell-type"));
        row.Add(DataCell("", _pwValDynamic,  "cell-val"));
        row.Add(DataCell("", PWArea, "cell-area"));

        // Celda Modificar
        var modCell = new VisualElement { name = "cell-mod" };
        modCell.style.width         = W(PWMod);
        modCell.style.flexShrink    = 0f;
        modCell.style.flexDirection = FlexDirection.Row;
        modCell.style.alignItems    = Align.Center;

        var dash = new Label("—") { name = "mod-dash" };
        dash.style.color          = ColMuted;
        dash.style.fontSize       = _fontSize;
        dash.style.unityTextAlign = TextAnchor.MiddleLeft;
        modCell.Add(dash);

        var btnBool = new Button { name = "mod-bool" };
        StyleButton(btnBool, ColBtnFalse, W(PWMod) - 4f);
        modCell.Add(btnBool);

        var tf = new TextField { name = "mod-tf" };
        tf.style.width       = W(PWMod) - 50f;
        tf.style.flexShrink  = 0f;
        tf.style.marginRight = 4f;
        StyleTextField(tf);
        modCell.Add(tf);

        var btnOk = new Button { text = "OK", name = "mod-ok" };
        StyleButton(btnOk, ColBtnOk, 42f);
        modCell.Add(btnOk);

        row.Add(modCell);
        return row; 
    }

    /// <summary>
    /// Rellena el contenedor visual instanciado en base a los datos de fila específicos por índice.
    /// </summary>
    private static void BindItem(
        VisualElement                 el,
        int                           index,
        List<RowData>                 items,
        Action<string, string>        onWrite,
        Action<string, VisualElement> onMenuClick)
    {
        if (index < 0 || index >= items.Count) return;
        RowData rd = items[index];

        var groupHeader = el.Q("group-header");
        var dataRow     = el.Q("data-row");

        if (rd.IsGroupHeader)
        {
            groupHeader.style.display = DisplayStyle.Flex;
            dataRow.style.display     = DisplayStyle.None;
            el.style.backgroundColor  = Color.clear;
            el.style.height           = GroupHeaderHeight;

            var chevron  = el.Q<Label>("group-chevron");
            var groupLbl = el.Q<Label>("group-label");
            var countLbl = el.Q<Label>("group-count");

            groupLbl.text  = rd.GroupLabel;
            groupLbl.style.fontSize = _fontSize;
            countLbl.style.fontSize = _fontSize - 1f;

            int count = 0;
            for (int j = index + 1; j < items.Count && !items[j].IsGroupHeader; j++) count++;
            countLbl.text  = rd.IsCollapsed ? $"({count} ocultos)" : $"({count})";
            chevron.text   = rd.IsCollapsed ? "▶" : "▼";

            if (groupHeader.userData is Action oldClick) groupHeader.UnregisterCallback<ClickEvent>(_ => oldClick());
            Action toggleCollapse = () => onMenuClick?.Invoke("__group__" + rd.GroupLabel, groupHeader);
            groupHeader.userData = toggleCollapse;
            groupHeader.RegisterCallback<ClickEvent>(_ => toggleCollapse());
        }
        else
        {
            groupHeader.style.display = DisplayStyle.None;
            dataRow.style.display     = DisplayStyle.Flex;

            el.style.backgroundColor = index % 2 == 0 ? ColRowEven : ColRowOdd;
            float rowH = Mathf.Max(24f, _fontSize * 2.4f);
            el.style.height = rowH;

            UpdateCellWidth(dataRow, "cell-menu", PWMenu);
            UpdateCellWidth(dataRow, "cell-name", _pwNameDynamic);
            UpdateCellWidth(dataRow, "cell-type", PWType);
            UpdateCellWidth(dataRow, "cell-val",  _pwValDynamic);
            UpdateCellWidth(dataRow, "cell-area", PWArea);
            UpdateModCellWidths(dataRow);

            SetLabel(dataRow, "cell-name", rd.Name);
            SetLabel(dataRow, "cell-type", rd.Type);
            SetLabel(dataRow, "cell-val",  rd.Value);
            SetLabel(dataRow, "cell-area", rd.Area, rd.Area == "E" ? ColAccent : ColMuted);

            var btnMenu = dataRow.Q<Button>("btn-menu");
            if (btnMenu.userData is Action oldMenu) btnMenu.clicked -= oldMenu;
            Action openMenu = () => onMenuClick?.Invoke(rd.Name, btnMenu);
            btnMenu.userData = openMenu;
            btnMenu.clicked += openMenu;

            var dash    = dataRow.Q<Label>("mod-dash");
            var btnBool = dataRow.Q<Button>("mod-bool");
            var tf      = dataRow.Q<TextField>("mod-tf");
            var btnOk   = dataRow.Q<Button>("mod-ok");

            dash.style.display    = DisplayStyle.None;
            btnBool.style.display = DisplayStyle.None;
            tf.style.display      = DisplayStyle.None;
            btnOk.style.display   = DisplayStyle.None;

            if (rd.Area == "S")
            {
                dash.style.display  = DisplayStyle.Flex;
                dash.style.fontSize = _fontSize;
            }
            else if (rd.Type == "Bool")
            {
                btnBool.style.display = DisplayStyle.Flex;
                btnOk.style.display   = DisplayStyle.Flex;
                bool active = rd.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || rd.Value == "1";
                btnBool.text                  = active ? "TRUE" : "FALSE";
                btnBool.style.backgroundColor = active ? ColBtnTrue : ColBtnFalse;
                btnBool.style.fontSize        = _fontSize;
                btnBool.style.width           = W(PWMod) - 50f;
                btnBool.style.height          = Mathf.Max(20f, _fontSize * 1.8f);
                btnOk.style.fontSize          = _fontSize;
                btnOk.style.height            = Mathf.Max(20f, _fontSize * 1.8f);

                if (btnBool.userData is Action oldBoolCb) btnBool.clicked -= oldBoolCb;
                Action toggle = () => onWrite(rd.Name, active ? "false" : "true");
                btnBool.userData = toggle;
                btnBool.clicked += toggle;

                if (btnOk.userData is Action oldOkCb) btnOk.clicked -= oldOkCb;
                Action confirm = () => onWrite(rd.Name, active ? "false" : "true");
                btnOk.userData = confirm;
                btnOk.clicked += confirm;
            }
            else
            {
                tf.style.display    = DisplayStyle.Flex;
                btnOk.style.display = DisplayStyle.Flex;
                tf.SetValueWithoutNotify(rd.EditBuffer);
                tf.style.width       = W(PWMod) - 50f;
                tf.style.fontSize    = _fontSize;
                tf.style.height      = Mathf.Max(20f, _fontSize * 1.8f);
                btnOk.style.fontSize = _fontSize;
                btnOk.style.height   = Mathf.Max(20f, _fontSize * 1.8f);

                if (tf.userData is EventCallback<ChangeEvent<string>> oldTfCb)
                    tf.UnregisterValueChangedCallback(oldTfCb);
                EventCallback<ChangeEvent<string>> onTfChange = evt => rd.EditBuffer = evt.newValue;
                tf.userData = onTfChange;
                tf.RegisterValueChangedCallback(onTfChange);

                if (btnOk.userData is Action oldOkCb) btnOk.clicked -= oldOkCb;
                Action confirm = () => onWrite(rd.Name, rd.EditBuffer);
                btnOk.userData = confirm;
                btnOk.clicked += confirm;
            }
        }
    }

    // -- Helpers de layout ------------------------------------------------------

    private static void UpdateCellWidth(VisualElement row, string name, float proportion)
    {
        var el = row.Q(name);
        if (el != null) el.style.width = W(proportion);
    }

    private static void UpdateModCellWidths(VisualElement row)
    {
        var modCell = row.Q("cell-mod");
        if (modCell == null) return;
        modCell.style.width = W(PWMod);
        var b = modCell.Q<Button>("mod-bool");
        if (b != null) b.style.width = W(PWMod) - 50f;
        var t = modCell.Q<TextField>("mod-tf");
        if (t != null) t.style.width = W(PWMod) - 50f;
    }

    // -- Helpers de estilo ------------------------------------------------------

    private static VisualElement MakeRow(Color bg)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.alignItems      = Align.Center;
        row.style.paddingLeft     = 8f;
        row.style.paddingRight    = 8f;
        row.style.backgroundColor = bg;
        return row;
    }

    private static Label HeaderCell(string text, float proportion)
    {
        var l = new Label(text);
        l.style.width                   = W(proportion);
        l.style.flexShrink              = 0f;
        l.style.color                   = ColAccent;
        l.style.fontSize                = _fontSize;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.unityTextAlign          = TextAnchor.MiddleLeft;
        return l;
    }

    private static Label DataCell(string text, float proportion, string name)
    {
        var l = new Label(text) { name = name };
        l.style.width          = W(proportion);
        l.style.flexShrink     = 0f;
        l.style.color          = ColText;
        l.style.fontSize       = _fontSize;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        l.style.overflow       = Overflow.Hidden;
        l.style.textOverflow   = TextOverflow.Ellipsis;
        l.style.whiteSpace     = WhiteSpace.NoWrap;

        return l;
    }

    private static void StyleButton(Button b, Color bg, float width)
    {
        float h = Mathf.Max(20f, _fontSize * 1.8f);
        b.style.width           = width;
        b.style.height          = h;
        b.style.flexShrink      = 0f;
        b.style.backgroundColor = bg;
        b.style.color           = Color.white;
        b.style.fontSize        = _fontSize;
        b.style.borderTopLeftRadius     = 3f; b.style.borderTopRightRadius    = 3f;
        b.style.borderBottomLeftRadius  = 3f; b.style.borderBottomRightRadius = 3f;
        b.style.borderTopWidth    = 0f; b.style.borderBottomWidth = 0f;
        b.style.borderLeftWidth   = 0f; b.style.borderRightWidth  = 0f;
    }

    private static void StyleTextField(TextField tf)
    {
        tf.style.backgroundColor = Color.clear;
        tf.style.borderTopWidth    = 0f; tf.style.borderBottomWidth = 0f;
        tf.style.borderLeftWidth   = 0f; tf.style.borderRightWidth  = 0f;

        tf.RegisterCallback<AttachToPanelEvent>(e => 
        {
            var input = tf.Q(className: "unity-text-field__input");
            if (input == null) return;

            input.style.backgroundColor = ColTfBg;
            input.style.color           = ColText;

            input.style.paddingTop    = 0f;
            input.style.paddingBottom = 0f;
            input.style.marginTop     = 0f;
            input.style.marginBottom  = 0f;
            
            input.style.borderTopLeftRadius     = 3f; input.style.borderTopRightRadius    = 3f;
            input.style.borderBottomLeftRadius  = 3f; input.style.borderBottomRightRadius = 3f;
            
            input.style.borderTopColor    = ColTfBorder; input.style.borderBottomColor = ColTfBorder;
            input.style.borderLeftColor   = ColTfBorder; input.style.borderRightColor  = ColTfBorder;
            input.style.borderTopWidth    = 1f; input.style.borderBottomWidth = 1f;
            input.style.borderLeftWidth   = 1f; input.style.borderRightWidth  = 1f;
        });
    }

    private static void SetLabel(VisualElement root, string name, string text, Color? color = null)
    {
        var l = root.Q<Label>(name);
        if (l == null) return;
        l.text       = text;
        l.style.color    = color ?? ColText;
        l.style.fontSize = _fontSize;
    }
}