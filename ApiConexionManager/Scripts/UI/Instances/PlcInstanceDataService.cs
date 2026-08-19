//----------------------------------------------------------------------------------------------------------------------
// PLC INSTANCE DATA SERVICE
//
// Desc: Servicio de datos (Singleton) encargado de gestionar el estado y las operaciones 
//       relacionadas con las instancias del PLC (carga, conexión, desconexión, run y stop).
//       Actúa como intermediario entre la UI y la ApiInterface.
//
// Autor: Alex Asensio (o tu nombre)
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

public class PlcInstanceDataService : MonoBehaviour
{
    // -- Singleton --------------------------------------------------------------

    public static PlcInstanceDataService Instance { get; private set; }

    // -- Estado Interno ---------------------------------------------------------

    public List<ApiInterface.PlcInstance> Instances { get; private set; } = new();
    public string StatusMessage { get; private set; } = "Pulsa actualizar para cargar";
    public string ConnectMessage { get; private set; } = "";

    // -- Configuración de Audio -------------------------------------------------

    [Header("Conexión a instancia")]
    public AudioClip connectSound;
    private AudioSource audioSource_conexion;

    [Header("RUN instancia")]
    public AudioClip runSound;
    private AudioSource audioSource_run;

    [Header("STOP instancia")]
    public AudioClip stopSound;
    private AudioSource audioSource_stop;

    // -- Eventos ----------------------------------------------------------------

    /// <summary>Se dispara al finalizar la solicitud de carga de instancias.</summary>
    public event Action OnInstancesLoaded;

    /// <summary>Se dispara cuando cambia el estado o mensaje de conexión (incluye Run/Stop).</summary>
    public event Action OnConnectionStatusChanged;

    /// <summary>Se dispara cuando cambia el estado o mensaje de desconexión.</summary>
    public event Action DisconnectionStatusChanged;

    // -- Ciclo de Vida Unity ----------------------------------------------------

    private void Awake()
    {
        // Comprobación de Singleton segura para el flujo de ejecución (Build vs Editor)
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        
        Instance = this;
        
        if (Application.isPlaying) 
            DontDestroyOnLoad(gameObject);

        // Inicialización de componentes de audio asociados al GameObject
        audioSource_conexion = gameObject.AddComponent<AudioSource>();
        audioSource_run      = gameObject.AddComponent<AudioSource>();
        audioSource_stop     = gameObject.AddComponent<AudioSource>();
    }

    // -- API Pública ------------------------------------------------------------

    /// <summary>
    /// Solicita la lista de instancias disponibles a la API.
    /// Si solo hay una instancia, intenta conectarse automáticamente a ella.
    /// </summary>
    public void LoadInstances()
    {
        StatusMessage = "Cargando instancias...";
        OnInstancesLoaded?.Invoke(); 

        ApiInterface.Instance.GetInstances(
            instances =>
            {
                Instances = instances;
                StatusMessage = $"{instances.Count} instancias · {DateTime.Now:HH:mm:ss}";
                OnInstancesLoaded?.Invoke();

                // Conexión automática si solo existe una instancia
                if (Instances.Count == 1)
                {
                    Debug.Log("Entro");
                    ConnectToInstance(Instances[0].ID.ToString());
                }
            },
            err =>
            {
                StatusMessage = "Error: " + err;
                OnInstancesLoaded?.Invoke();
            }
        );
    }

    /// <summary>
    /// Inicia la conexión a una instancia específica del PLC mediante su ID.
    /// </summary>
    /// <param name="instanceId">ID de la instancia destino.</param>
    public void ConnectToInstance(string instanceId)
    {
        ConnectMessage = $"Conectando a '{instanceId}'...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.ConnectInstance(
            instanceId,
            msg => 
            { 
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();

                // Reproducir feedback de audio de conexión exitosa
                if (connectSound != null) 
                    audioSource_conexion.PlayOneShot(connectSound);
            },
            err => 
            { 
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Cierra la conexión actual con la instancia del PLC.
    /// </summary>
    public void DisconnectInstance()
    {
        ConnectMessage = $"Desconectando de instancia ...";
        DisconnectionStatusChanged?.Invoke();

        ApiInterface.Instance.DisconnectInstance(
            msg => 
            { 
                ConnectMessage = "OK · " + msg;
                DisconnectionStatusChanged?.Invoke();
            },
            err => 
            { 
                ConnectMessage = "Error: " + err;
                DisconnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Envía el comando para poner el PLC virtual en modo RUN.
    /// </summary>
    public void Run()
    {
        ConnectMessage = "Poniendo en RUN...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.RunInstance(
            msg =>
            {
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();

                if (runSound != null) 
                    audioSource_run.PlayOneShot(runSound);
            },
            err =>
            {
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Envía el comando para detener el PLC virtual (modo STOP).
    /// </summary>
    public void Stop()
    {
        ConnectMessage = "Poniendo en STOP...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.StopInstance(
            msg =>
            {
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();

                if (stopSound != null) 
                    audioSource_run.PlayOneShot(stopSound);
            },
            err =>
            {
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }
}