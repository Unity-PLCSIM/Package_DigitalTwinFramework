//----------------------------------------------------------------------------------------------------------------------
// INTERFAZ API PLC SIMULATION, TIA PORTAL
//
// Desc: Interfaz que abstrae la comunicacion con la API a métodos que pueden ser llamados por el resto
// de elementos de la simulación, sin que estos conozcan la estructura de dicha API.
//
// Coms:  - Se debe poner la opción 'Always Allowed' en Edit->Project Settings->Player->Allow downloads over HTTP
//        - Se debe ajustar 'baseUrl' a la dirección IP y PUERTO donde corra la API
//        - Se debe ajustar 'wsUrl' a la dirección WebSocket y PUERTO donde corra la API
//        - Requiere NativeWebSocket: Window > Package Manager > Add from git URL:
//          https://github.com/endel/NativeWebSocket.git#upm
//
// Uso:
//   // Leer un tag
//   ApiInterface.Instance.GetTag("Motor", "Bool", (value) => Debug.Log("Motor: " + value));
//
//   // Escribir un tag
//   ApiInterface.Instance.SetTag("Marcha", "Bool", "true", (msg) => Debug.Log(msg));
//
//   // Obtener todos los tags disponibles
//   ApiInterface.Instance.GetAllTags((tags) => { foreach (var t in tags) Debug.Log(t.Name); });
//
//   // Recibir cambios de tags de SALIDA en tiempo real por WebSocket
//   ApiInterface.Instance.SubscribeOutputTag("Motor", (value) => Debug.Log("Motor: " + value));
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.Networking;

public class ApiInterface : MonoBehaviour
{
    // -- Singleton --------------------------------------------------------------
    public static ApiInterface Instance { get; private set; }

    // -- Configuración ----------------------------------------------------------
    [Header("Configuración API")]
    public string baseUrl = "http://192.168.49.128:9000/api/plc";

    [Tooltip("URL WebSocket del servidor. Debe apuntar a ws://<ip>:9001/ws/tags")]
    public string wsUrl = "ws://192.168.49.128:9001/ws/tags";

    [Tooltip("Segundos entre cada ciclo de polling automático (0 = desactivado)")]
    public float pollInterval = 0.5f;

    [Tooltip("Segundos entre intentos de reconexión WebSocket")]
    public float wsReconnectInterval = 3f;

    [Header("Conectado con una instancia")]
    public bool connected = false;

    // -- Modelos de datos -------------------------------------------------------

    [Serializable]
    public class TagInfo
    {
        public string Name;
        public string Type;
    }

    [Serializable]
    public class TagValue
    {
        public string Tag;
        public string Value;
    }

    [Serializable]
    public class TagWithValue
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;
    }

    [Serializable]
    public class TagWritePayload
    {
        public string Value;
        public string Type;
    }

    [Serializable]
    public class ApiMessage
    {
        public string Message;
    }

    [Serializable]
    public class PlcInstance
    {
        public int ID;
        public string Name;
    }

    [Serializable]
    private class TagInfoList      { public List<TagInfo>      items; }

    [Serializable]
    private class TagWithValueList { public List<TagWithValue> items; }

    [Serializable]
    private class PlcInstanceList  { public List<PlcInstance>  items; }

    // -- Eventos globales (opcionales) ------------------------------------------

    /// <summary>Se dispara cada vez que una petición falla.</summary>
    public event Action<string> OnApiError;

    /// <summary>Se dispara cuando el WebSocket se conecta correctamente.</summary>
    public event Action OnWsConnected;

    /// <summary>Se dispara cuando el WebSocket se desconecta.</summary>
    public event Action OnWsDisconnected;

    // -- WebSocket --------------------------------------------------------------

    private WebSocket _ws;
    private bool _wsConnecting = false;

    private readonly Dictionary<string, Action<string>> _outputSubscriptions
        = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);

    // Cola thread-safe para sincronizar mensajes del hilo WS al hilo principal de Unity
    private readonly ConcurrentQueue<(string name, string value)> _pendingWsChanges
        = new ConcurrentQueue<(string, string)>();

    // -- Ciclo de vida Unity ----------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (pollInterval > 0f)
            StartCoroutine(AutoPollCoroutine());

        StartCoroutine(ConnectWebSocketCoroutine());
    }

    private void Update()
    {
        // Despacho de mensajes requerido por NativeWebSocket para ejecutar callbacks en Unity
        _ws?.DispatchMessageQueue();

        // Consumo seguro de cambios encolados desde el hilo de WebSocket
        while (_pendingWsChanges.TryDequeue(out var change))
        {
            if (_outputSubscriptions.TryGetValue(change.name, out Action<string> callback))
                callback?.Invoke(change.value);
        }
    }

    private async void OnDestroy()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
            await _ws.Close();
    }

    // -- API PÚBLICA ------------------------------------------------------------

    /// <summary>
    /// Obtiene la lista completa de tags disponibles en el PLC.
    /// </summary>
    /// <param name="onSuccess">Recibe la lista de TagInfo con Name y Type.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void GetAllTags(Action<List<TagInfo>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags";
        StartCoroutine(GetRequest(url, (json) =>
        {
            // Wrapper necesario ya que JsonUtility no parsea arrays en la raíz
            string wrapped = "{\"items\":" + json + "}";
            TagInfoList result = JsonUtility.FromJson<TagInfoList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Obtiene todos los tags con su tipo y valor en una sola petición.
    /// </summary>
    public void GetTagsWithValues(Action<List<TagWithValue>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags-with-values";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            TagWithValueList result = JsonUtility.FromJson<TagWithValueList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Obtiene todos los tags tipo SALIDA con su tipo y valor en una sola petición.
    /// </summary>
    public void GetOutputTagsWithValues(Action<List<TagWithValue>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/output-tags-with-values";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            TagWithValueList result = JsonUtility.FromJson<TagWithValueList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Lee el valor actual de un tag del PLC.
    /// </summary>
    /// <param name="tagName">Nombre exacto del tag (ej: "Motor").</param>
    /// <param name="tagType">Tipo del tag (ej: "Bool", "DInt (Int32)").</param>
    /// <param name="onSuccess">Recibe el valor como string (ej: "True", "42").</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void GetTag(string tagName, string tagType, Action<string> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags/{UnityWebRequest.EscapeURL(tagName)}?type={UnityWebRequest.EscapeURL(tagType)}";
        StartCoroutine(GetRequest(url, (json) =>
        {
            TagValue result = JsonUtility.FromJson<TagValue>(json);
            onSuccess?.Invoke(result.Value);
        }, onError));
    }

    /// <summary>
    /// Lee el valor de un tag y lo devuelve ya convertido a bool.
    /// Solo para tags de tipo Bool.
    /// </summary>
    public void GetTagBool(string tagName, Action<bool> onSuccess, Action<string> onError = null)
    {
        GetTag(tagName, "Bool", (value) =>
        {
            onSuccess?.Invoke(value.Equals("True", StringComparison.OrdinalIgnoreCase));
        }, onError);
    }

    /// <summary>
    /// Lee el valor de un tag y lo devuelve ya convertido a int.
    /// Para tags de tipo DInt, Int, UDInt, UInt, etc.
    /// </summary>
    public void GetTagInt(string tagName, string tagType, Action<int> onSuccess, Action<string> onError = null)
    {
        GetTag(tagName, tagType, (value) =>
        {
            if (int.TryParse(value, out int intValue))
                onSuccess?.Invoke(intValue);
            else
                HandleError($"No se pudo convertir '{value}' a int para el tag '{tagName}'", onError);
        }, onError);
    }

    /// <summary>
    /// Escribe un valor en un tag del PLC.
    /// </summary>
    /// <param name="tagName">Nombre exacto del tag (ej: "Marcha").</param>
    /// <param name="tagType">Tipo del tag (ej: "Bool").</param>
    /// <param name="value">Valor a escribir como string (ej: "true", "42").</param>
    /// <param name="onSuccess">Recibe el mensaje de confirmación de la API.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void SetTag(string tagName, string tagType, string value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags/{UnityWebRequest.EscapeURL(tagName)}";
        TagWritePayload payload = new TagWritePayload { Value = value, Type = tagType };
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(PutRequest(url, json, (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    /// <summary>
    /// Escribe un valor bool en un tag del PLC. Atajo para SetTag con tipo Bool.
    /// </summary>
    public void SetTagBool(string tagName, bool value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        SetTag(tagName, "Bool", value.ToString().ToLower(), onSuccess, onError);
    }

    /// <summary>
    /// Escribe un valor entero en un tag del PLC.
    /// </summary>
    public void SetTagInt(string tagName, string tagType, int value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        SetTag(tagName, tagType, value.ToString(), onSuccess, onError);
    }

    /// <summary>
    /// Obtiene la lista de instancias PLC disponibles.
    /// </summary>
    public void GetInstances(Action<List<PlcInstance>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            PlcInstanceList result = JsonUtility.FromJson<PlcInstanceList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Conecta a una instancia PLC por su ID.
    /// </summary>
    public void ConnectInstance(string instanceId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances/{UnityWebRequest.EscapeURL(instanceId)}/connect";
        StartCoroutine(PostRequest(url, "", (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            connected = true;
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    /// <summary>
    /// Desconecta la instancia PLC a la que estábamos suscritos.
    /// </summary>
    public void DisconnectInstance(Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances/disconnect";
        StartCoroutine(PostRequest(url, "", (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            connected = false;
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    /// <summary>
    /// Pone el PLC en modo RUN.
    /// </summary>
    public void RunInstance(Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances/run";
        StartCoroutine(PostRequest(url, "", (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    /// <summary>
    /// Pone el PLC en modo STOP.
    /// </summary>
    public void StopInstance(Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances/stop";
        StartCoroutine(PostRequest(url, "", (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    // -- WEBSOCKET — Conexión y reconexión --------------------------------------

    /// <summary>
    /// Corrutina que mantiene la conexión WebSocket viva y reintenta ante caídas.
    /// </summary>
    private IEnumerator ConnectWebSocketCoroutine()
    {
        while (true)
        {
            if (_ws == null || _ws.State == WebSocketState.Closed)
                yield return StartCoroutine(OpenWebSocket());

            yield return new WaitForSeconds(wsReconnectInterval);
        }
    }

    /// <summary>
    /// Abre la conexión WebSocket y registra los callbacks de ciclo de vida.
    /// </summary>
    private IEnumerator OpenWebSocket()
    {
        if (_wsConnecting) yield break;
        _wsConnecting = true;

        _ws = new WebSocket(wsUrl);

        _ws.OnOpen += () =>
        {
            Debug.Log("[ApiInterface] WebSocket conectado.");
            _wsConnecting = false;
            OnWsConnected?.Invoke();
        };

        _ws.OnMessage += OnWsMessage;

        _ws.OnError += (err) =>
        {
            Debug.LogWarning($"[ApiInterface] WebSocket error: {err}");
            _wsConnecting = false;
        };

        _ws.OnClose += (code) =>
        {
            Debug.Log($"[ApiInterface] WebSocket cerrado (código {code}).");
            _wsConnecting = false;
            OnWsDisconnected?.Invoke();
        };

        var connectTask = _ws.Connect();
        yield return new WaitUntil(() => connectTask.IsCompleted);
    }

    /// <summary>
    /// Procesa mensajes entrantes del servidor WS y los encola para el hilo principal.
    /// </summary>
    private void OnWsMessage(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        Debug.Log($"[WS] JSON crudo: {json}");

        string wrapped = "{\"items\":" + json + "}";

        TagWithValueList changes;
        try { changes = JsonUtility.FromJson<TagWithValueList>(wrapped); }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ApiInterface] Error parseando mensaje WS: {ex.Message}");
            return;
        }

        if (changes?.items == null) return;

        foreach (TagWithValue change in changes.items)
            _pendingWsChanges.Enqueue((change.Name, change.Value));
    }

    // -- WEBSOCKET — API Pública ------------------------------------------------

    /// <summary>
    /// Suscribe un tag de SALIDA para recibir sus cambios por WebSocket.
    /// </summary>
    /// <param name="tagName">Nombre exacto del tag (ej: "Motor").</param>
    /// <param name="onValue">Callback que recibe el nuevo valor como string.</param>
    public void SubscribeOutputTag(string tagName, Action<string> onValue)
    {
        _outputSubscriptions[tagName] = onValue;
    }

    /// <summary>
    /// Cancela la suscripción WebSocket de un tag de salida.
    /// </summary>
    public void UnsubscribeOutputTag(string tagName)
    {
        _outputSubscriptions.Remove(tagName);
    }

    public bool IsWsConnected => _ws != null && _ws.State == WebSocketState.Open;

    // -- POLLING AUTOMÁTICO -----------------------------------------------------

    private readonly List<(string name, string type, Action<string> callback)> _polledTags = new();

    /// <summary>
    /// Suscribe un tag de ENTRADA para ser leído automáticamente mediante polling HTTP.
    /// </summary>
    public void SubscribeTag(string tagName, string tagType, Action<string> onValue)
    {
        _polledTags.Add((tagName, tagType, onValue));
    }

    /// <summary>
    /// Elimina la suscripción de polling HTTP de un tag.
    /// </summary>
    public void UnsubscribeTag(string tagName)
    {
        _polledTags.RemoveAll(t => t.name == tagName);
    }

    private IEnumerator AutoPollCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            foreach (var (name, type, callback) in _polledTags)
                GetTag(name, type, callback);
        }
    }

    // -- CORRUTINAS INTERNAS (HTTP) ---------------------------------------------

    private IEnumerator GetRequest(string url, Action<string> onSuccess, Action<string> onError)
    {
        if (!connected && !url.Contains("/instances")) { HandleError("No conectado a ninguna instancia.", onError); yield break; }
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(request.downloadHandler.text);
        else
            HandleError($"GET {url} → {request.responseCode} {request.error}", onError);
    }

    private IEnumerator PutRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        if (!connected && !url.Contains("/instances")) { HandleError("No conectado a ninguna instancia.", onError); yield break; }
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest request = new UnityWebRequest(url, "PUT");
        request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept",       "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(request.downloadHandler.text);
        else
            HandleError($"PUT {url} → {request.responseCode} {request.error}", onError);
    }

    private IEnumerator PostRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        if (!connected && !url.Contains("/instances")) { HandleError("No conectado a ninguna instancia.", onError); yield break; }
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept",       "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(request.downloadHandler.text);
        else
            HandleError($"POST {url} → {request.responseCode} {request.error}", onError);
    }

    private void HandleError(string message, Action<string> localHandler)
    {
        Debug.LogError($"[ApiInterface] {message}");
        localHandler?.Invoke(message);
        OnApiError?.Invoke(message);
    }
}