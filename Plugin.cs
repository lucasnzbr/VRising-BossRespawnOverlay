using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using ProjectM;
using ProjectM.Network;
using ProjectM.UI;
using SangrisInterface.Patches;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRespawnOverlay;

internal sealed class BossDefinition
{
    internal BossDefinition(string displayName, int level, string? commandName = null)
    {
        var resolvedCommandName = commandName ?? displayName.ToLowerInvariant();
        if (resolvedCommandName.Equals("bar~ao", StringComparison.OrdinalIgnoreCase))
        {
            resolvedCommandName = "bar\u00e3o";
            displayName = "Bar\u00e3o";
        }

        DisplayName = displayName;
        Level = level;
        CommandName = resolvedCommandName;
    }

    internal string DisplayName { get; }
    internal int Level { get; }
    internal string CommandName { get; }
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("SangrisInterface", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "sangriafalls.vrising.bossrespawnoverlay";
    public const string PluginName = "Boss Respawn Overlay";
    public const string PluginVersion = "0.4.6";

    internal static readonly BossDefinition[] DefaultBosses =
    [
        new("Keely", 30),
        new("Errol", 30),
        new("Rufus", 30),
        new("Grayson", 37),
        new("Goreswine", 37),
        new("Lidia", 40),
        new("Clive", 40),
        new("Finn", 42),
        new("Polora", 45),
        new("Kodia", 45),
        new("Nicolau", 45),
        new("Quincey", 47),
        new("Beatrice", 50),
        new("Vincent", 54),
        new("Christina", 54),
        new("Tristan", 54),
        new("Erwin", 56),
        new("Kriig", 57),
        new("Leandra", 57),
        new("Maja", 57),
        new("Bane", 60),
        new("Grethel", 60),
        new("Meredith", 60),
        new("Terah", 63),
        new("Frostmaw", 63),
        new("Elena", 63),
        new("Gaius", 65),
        new("Cassius", 67),
        new("Jade", 67),
        new("Raziel", 67),
        new("Octavian", 68),
        new("Ziva", 70),
        new("Domina", 70),
        new("Angram", 71),
        new("Ungora", 73),
        new("Ben", 73),
        new("Foulrot", 73),
        new("Albert", 74),
        new("Willfred", 74),
        new("Cyril", 75),
        new("Magnus", 76),
        new("Barão", 80, "bar~ao"),
        new("Morian", 80),
        new("Mairwyn", 80),
        new("Henry", 84),
        new("Jakira", 85),
        new("Stavros", 85),
        new("Lucile", 86),
        new("Matka", 86),
        new("Terrorclaw", 86),
        new("Azariel", 89),
        new("Voltatia", 89),
        new("Simon", 90),
        new("Dantos", 92),
        new("Styx", 94),
        new("Gorecrusher", 94),
        new("Valencia", 94),
        new("Solarus", 96),
        new("Talzur", 96),
        new("Megara", 98),
        new("Adam", 98)
    ];

    internal static Plugin Instance { get; private set; } = null!;
    internal static BossRespawnOverlayBehaviour Behaviour { get; private set; } = null!;

    private Harmony _harmony = null!;
    private GameObject? _host;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<float> PollIntervalSeconds { get; private set; } = null!;
    internal static ConfigEntry<float> InitialDelaySeconds { get; private set; } = null!;
    internal static ConfigEntry<float> RightOffset { get; private set; } = null!;
    internal static ConfigEntry<float> TopOffset { get; private set; } = null!;
    internal static ConfigEntry<float> PanelWidth { get; private set; } = null!;
    internal static ConfigEntry<float> PanelHeight { get; private set; } = null!;
    internal static ConfigEntry<float> UiScale { get; private set; } = null!;
    internal static ConfigEntry<float> PositionX { get; private set; } = null!;
    internal static ConfigEntry<float> PositionY { get; private set; } = null!;
    internal static ConfigEntry<int> FontSize { get; private set; } = null!;
    internal static ConfigEntry<string> Bosses { get; private set; } = null!;
    internal static ConfigEntry<string> PinnedBosses { get; private set; } = null!;
    internal static ConfigEntry<string> ExpandedActs { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Exibe o contador no cliente.");
        PollIntervalSeconds = Config.Bind("General", "PollIntervalSeconds", 30f, "Intervalo entre ciclos completos de consulta.");
        InitialDelaySeconds = Config.Bind("General", "InitialDelaySeconds", 5f, "Atraso da primeira consulta depois de entrar no mundo.");
        Bosses = Config.Bind("Boss", "Bosses", string.Join(',', DefaultBosses.Select(boss => boss.CommandName)), "Bosses consultados, separados por vírgula e na ordem desejada.");
        RightOffset = Config.Bind("UI", "RightOffset", 28f, "Distância da borda direita em pixels.");
        TopOffset = Config.Bind("UI", "TopOffset", 28f, "Distância da borda superior em pixels.");
        PanelWidth = Config.Bind("UI", "PanelWidth", 420f, "Largura do painel em pixels.");
        PanelHeight = Config.Bind("UI", "PanelHeight", 650f, "Altura do painel em pixels; a lista rola quando necessário.");
        UiScale = Config.Bind("UI", "UiScale", 1f, "Escala visual; o botão alterna entre 60%, 75%, 85%, 100%, 115%, 125%, 150% e 175%.");
        PositionX = Config.Bind("UI", "PositionX", -1f, "Posição X salva; -1 usa o canto superior direito.");
        PositionY = Config.Bind("UI", "PositionY", -1f, "Posição Y salva; -1 usa o topo.");
        FontSize = Config.Bind("UI", "FontSize", 16, "Tamanho da fonte do contador.");
        ExpandedActs = Config.Bind("UI", "ExpandedActs", string.Empty, "Atos abertos na overlay, por exemplo: 1,3.");
        PinnedBosses = Config.Bind("Boss", "PinnedBosses", string.Empty, "Bosses preferenciais que aparecem no topo, separados por vírgula.");

        // Migra a lista curta usada pelo protótipo anterior para a lista completa.
        if (string.Equals(Bosses.Value.Trim(), "voltatia,ungora,albert,cyril", StringComparison.OrdinalIgnoreCase))
        {
            Bosses.Value = string.Join(',', DefaultBosses.Select(boss => boss.CommandName));
            Config.Save();
        }

        // Corrige o identificador antigo do Willfred e a grafia aproximada do Barão.
        var correctedBosses = string.Join(',', Bosses.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim().Equals("wilfred", StringComparison.OrdinalIgnoreCase)
                ? "willfred"
                : name.Trim().Equals("bar~ao", StringComparison.OrdinalIgnoreCase)
                    ? "bar\u00e3o"
                    : name.Trim()));
        if (!string.Equals(Bosses.Value, correctedBosses, StringComparison.Ordinal))
        {
            Bosses.Value = correctedBosses;
            Config.Save();
        }

        // A configuração anterior padrão tinha apenas os 30 bosses de nível 70+.
        // Se ela ainda estiver intacta, amplia para a lista completa nova sem
        // sobrescrever uma seleção personalizada do usuário.
        var previousDefault = DefaultBosses
            .Skip(DefaultBosses.Length - 30)
            .Select(boss => boss.CommandName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredNames = Bosses.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToList();
        if (configuredNames.Count == previousDefault.Count &&
            configuredNames.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(previousDefault))
        {
            Bosses.Value = string.Join(',', DefaultBosses.Select(boss => boss.CommandName));
            Config.Save();
        }

        ClassInjector.RegisterTypeInIl2Cpp<BossRespawnOverlayBehaviour>();
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        _host = new GameObject("BossRespawnOverlayHost");
        _host.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_host);
        Behaviour = _host.AddComponent<BossRespawnOverlayBehaviour>();

        Log.LogInfo($"{PluginName} {PluginVersion} carregado; bosses: {Bosses.Value}; polling: {PollIntervalSeconds.Value:0.#} s.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        if (_host != null)
        {
            Object.Destroy(_host);
            _host = null;
        }

        Behaviour = null!;
        return true;
    }

    internal static void LogUnknownMessage(string message)
    {
        Instance.Log.LogWarning($"Resposta de boss não reconhecida: {message}");
    }
}

internal sealed class BossRespawnOverlayBehaviour : MonoBehaviour
{
    private const float RequestTimeoutSeconds = 12f;
    // O servidor tolera a consulta individual, mas pode ignorar mensagens
    // quando várias chegam em sequência muito rápida. Um boss por segundo
    // completa a lista de 30 dentro de um ciclo de aproximadamente 30 s.
    private const float GapBetweenBossQueriesSeconds = 1f;

    private static ComponentType[]? _networkEventComponents;

    private static ComponentType[] GetNetworkEventComponents()
    {
        return _networkEventComponents ??= new ComponentType[]
        {
            ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
            ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
            ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
            ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>())
        };
    }

    private static readonly NetworkEventType ChatNetworkEventType = new()
    {
        IsAdminEvent = false,
        EventId = NetworkEvents.EventId_ChatMessageEvent,
        IsDebugEvent = false
    };

    internal static BossRespawnOverlayBehaviour? Instance { get; private set; }

    private sealed class BossState
    {
        internal BossState(int index, BossDefinition definition)
        {
            Index = index;
            Definition = definition;
        }

        internal int Index { get; }
        internal BossDefinition Definition { get; }
        internal string CommandName => Definition.CommandName;
        internal string DisplayName => Definition.DisplayName;
        internal int Level => Definition.Level;
        internal bool HasResponse { get; set; }
        internal bool IsAlive { get; set; }
        internal bool HasError { get; set; }
        internal bool IsPinned { get; set; }
        internal float RemainingSeconds { get; set; }
    }

    private readonly List<BossState> _bosses = new();
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _toggleStyle;
    private GUIStyle? _killButtonStyle;
    private GUIStyle? _pinButtonStyle;
    private GUIStyle? _sectionStyle;
    private GUIStyle? _resizeStyle;
    private float _nextQueryAt;
    private float _requestSentAt = -1f;
    private int _activeBossIndex = -1;
    private int _nextBossIndex;
    private int _nextPinnedIndex;
    private int _forcedBossIndex = -1;
    private bool _preferentialTurn = true;
    private bool _loggedUnknownResponse;
    private bool _overlayShown;
    private readonly bool[] _expandedActs = new bool[4];
    private World? _clientWorld;
    private Vector2 _panelPosition;
    private Vector2 _scrollPosition;
    private bool _panelPositionInitialized;
    private bool _draggingPanel;
    private Vector2 _dragOffset;

    private int OverlayFontSize => Mathf.Clamp(Plugin.FontSize.Value, 12, 16);
    private float UiScale => Mathf.Clamp(Plugin.UiScale.Value, 0.6f, 1.75f);
    private static readonly float[] UiScalePresets = { 0.6f, 0.75f, 0.85f, 1f, 1.15f, 1.25f, 1.5f, 1.75f };

    private BossState? ActiveBoss => _activeBossIndex >= 0 && _activeBossIndex < _bosses.Count
        ? _bosses[_activeBossIndex]
        : null;

    private void Awake()
    {
        Instance = this;
        LoadBosses();
        foreach (var token in Plugin.ExpandedActs.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token.Trim(), out var act) && act >= 1 && act <= 4)
            {
                _expandedActs[act - 1] = true;
            }
        }

        Plugin.Instance.Log.LogInfo($"Lista de bosses consultada ({_bosses.Count}): {string.Join(" -> ", _bosses.Select(boss => $"{boss.DisplayName} ({boss.Level}) [.boss tempo {boss.CommandName}]"))}");
        _nextQueryAt = Time.unscaledTime + Mathf.Max(0.5f, Plugin.InitialDelaySeconds.Value);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadBosses()
    {
        _bosses.Clear();
        var configured = Plugin.Bosses.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < configured.Length; i++)
        {
            var commandName = configured[i].Trim().ToLowerInvariant();
            if (commandName.Length == 0)
            {
                continue;
            }

            var definition = Plugin.DefaultBosses.FirstOrDefault(
                boss => string.Equals(boss.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                definition = new BossDefinition(commandName, 0);
            }

            _bosses.Add(new BossState(_bosses.Count, definition));
        }

        if (_bosses.Count == 0)
        {
            _bosses.Add(new BossState(0, Plugin.DefaultBosses[20]));
        }

        var pinned = new HashSet<string>(
            Plugin.PinnedBosses.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        foreach (var boss in _bosses)
        {
            boss.IsPinned = pinned.Contains(boss.CommandName);
        }
    }

    private static int GetActNumber(BossState boss)
    {
        return boss.Level <= 47 ? 1 :
               boss.Level <= 68 ? 2 :
               boss.Level <= 75 ? 3 : 4;
    }

    private static string GetActTitle(int act)
    {
        return act switch
        {
            1 => "Ato 1  (níveis 30–47)",
            2 => "Ato 2  (níveis 50–68)",
            3 => "Ato 3  (níveis 70–75)",
            _ => "Ato 4  (níveis 76+)",
        };
    }

    private List<BossState> GetActBosses(int act)
    {
        return _bosses
            .Where(boss => GetActNumber(boss) == act && !boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
    }

    private void ToggleAct(int act)
    {
        _expandedActs[act - 1] = !_expandedActs[act - 1];
        Plugin.ExpandedActs.Value = string.Join(',', Enumerable.Range(1, 4).Where(number => _expandedActs[number - 1]));
        Plugin.Instance.Config.Save();
    }

    private void DrawBossRow(BossState boss, float rowY, float rowWidth, float rowHeight, float killButtonWidth, float pinButtonWidth)
    {
        var labelWidth = rowWidth - killButtonWidth - pinButtonWidth - 12f;
        var colour = !boss.HasResponse ? "#D0D0D0" : boss.IsAlive ? "#55FF77" : "#FF5555";
        var label = $"<color={colour}><b>{boss.DisplayName} ({boss.Level})</b>: {GetBossStatusText(boss)}</color>";
        GUI.Label(new Rect(4f, rowY, labelWidth, rowHeight), label, _labelStyle);

        if (GUI.Button(
                new Rect(rowWidth - killButtonWidth - pinButtonWidth - 4f, rowY + 1f, killButtonWidth, rowHeight - 2f),
                "Morto",
                _killButtonStyle))
        {
            MarkBossKilled(boss);
        }

        if (GUI.Button(
                new Rect(rowWidth - pinButtonWidth, rowY + 1f, pinButtonWidth, rowHeight - 2f),
                boss.IsPinned ? "Topo" : "Fixar",
                _pinButtonStyle))
        {
            TogglePinned(boss);
        }
    }

    private void Update()
    {
        if (!Plugin.Enabled.Value)
        {
            return;
        }

        foreach (var boss in _bosses)
        {
            if (boss.HasResponse && !boss.IsAlive && !boss.HasError && boss.RemainingSeconds > 0f)
            {
                boss.RemainingSeconds = Mathf.Max(0f, boss.RemainingSeconds - Time.unscaledDeltaTime);
                if (boss.RemainingSeconds <= 0f)
                {
                    boss.RemainingSeconds = 0f;
                    boss.IsAlive = true;
                }
            }
        }

        var activeBoss = ActiveBoss;
        if (activeBoss != null && Time.unscaledTime - _requestSentAt > RequestTimeoutSeconds)
        {
            Plugin.Instance.Log.LogWarning($"O comando .boss tempo {activeBoss.CommandName} não respondeu dentro do timeout.");
            CompleteActiveRequest();
        }

        if (ActiveBoss == null && Time.unscaledTime >= _nextQueryAt)
        {
            TrySendBossQuery();
        }
    }

    internal void HandleChatUpdate(ClientChatSystem chatSystem)
    {
        // ClientChatPatch.LocalUser/LocalCharacter pertencem ao mundo do chat.
        // Uma Entity nunca pode ser consultada por outro EntityManager/world.
        _clientWorld = chatSystem.World;

        var activeBoss = ActiveBoss;
        if (activeBoss == null)
        {
            return;
        }

        NativeArray<Entity> entities = default;
        try
        {
            entities = chatSystem._ReceiveChatMessagesQuery.ToEntityArray(Allocator.Temp);
            var entityManager = chatSystem.World.EntityManager;
            var completed = false;

            foreach (var entity in entities)
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<ChatMessageServerEvent>(entity))
                {
                    continue;
                }

                var message = entityManager.GetComponentData<ChatMessageServerEvent>(entity);
                var text = message.MessageText.Value;
                if (!LooksLikeBossResponse(text, activeBoss))
                {
                    continue;
                }

                if (TryApplyResponse(text, activeBoss, out var responseCompleted))
                {
                    completed |= responseCompleted;
                    _loggedUnknownResponse = false;
                }
                else if (!_loggedUnknownResponse)
                {
                    _loggedUnknownResponse = true;
                    Plugin.LogUnknownMessage(text);
                }

                // A consulta e as linhas de resposta são mensagens do protocolo
                // de chat. Removê-las aqui impede que apareçam no chat do jogador.
                entityManager.DestroyEntity(entity);
            }

            if (completed)
            {
                CompleteActiveRequest();
            }
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao ler resposta de boss: {ex}");
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }
        }
    }

    internal void NotifyManualChatCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !text.TrimStart().StartsWith(".boss tempo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Uma resposta manual nunca deve ser consumida pelo overlay. Se ela
        // acontecer durante uma consulta automática, cancelamos esta consulta;
        // sem um request-id no protocolo do servidor, as duas respostas seriam
        // indistinguíveis depois que chegassem ao cliente.
        if (ActiveBoss != null)
        {
            Plugin.Instance.Log.LogDebug("Consulta automática cancelada para preservar resposta de comando manual.");
            CompleteActiveRequest();
        }
    }

    private List<BossState> GetPinnedBossesForPolling()
    {
        return _bosses
            .Where(boss => boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
    }

    private BossState? SelectNextScheduledBoss(out bool isPreferred)
    {
        var pinnedBosses = GetPinnedBossesForPolling();
        var hasNormalBoss = _bosses.Any(boss => !boss.IsPinned);

        if (pinnedBosses.Count > 0 && (_preferentialTurn || !hasNormalBoss))
        {
            isPreferred = true;
            return pinnedBosses[_nextPinnedIndex % pinnedBosses.Count];
        }

        for (var offset = 0; offset < _bosses.Count; offset++)
        {
            var index = (_nextBossIndex + offset) % _bosses.Count;
            if (!_bosses[index].IsPinned)
            {
                isPreferred = false;
                return _bosses[index];
            }
        }

        isPreferred = true;
        return pinnedBosses.Count > 0
            ? pinnedBosses[_nextPinnedIndex % pinnedBosses.Count]
            : null;
    }

    private void AdvanceScheduledBoss(BossState boss, bool wasPreferred)
    {
        if (wasPreferred)
        {
            var pinnedCount = _bosses.Count(item => item.IsPinned);
            _nextPinnedIndex = pinnedCount == 0 ? 0 : (_nextPinnedIndex + 1) % pinnedCount;
            // Havendo bosses normais, a próxima consulta deve alternar para a fila normal.
            _preferentialTurn = !_bosses.Any(item => !item.IsPinned);
            return;
        }

        _nextBossIndex = (boss.Index + 1) % _bosses.Count;
        // Depois de um boss normal, volta para os preferenciais quando existirem.
        _preferentialTurn = _bosses.Any(item => item.IsPinned);
    }

    private void TrySendBossQuery()
    {
        var localCharacter = ClientChatPatch.LocalCharacter;
        var localUser = ClientChatPatch.LocalUser;
        if (localCharacter == Entity.Null || localUser == Entity.Null)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var world = _clientWorld;
        if (world == null || !world.IsCreated)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var isForced = _forcedBossIndex >= 0;
        var isPreferred = false;
        var boss = isForced
            ? _bosses[_forcedBossIndex]
            : SelectNextScheduledBoss(out isPreferred);
        if (boss == null)
        {
            _nextQueryAt = Time.unscaledTime + 2f;
            return;
        }

        var command = $".boss tempo {boss.CommandName}";
        try
        {
            var entityManager = world.EntityManager;
            if (!entityManager.HasComponent<NetworkId>(localUser))
            {
                _nextQueryAt = Time.unscaledTime + 2f;
                return;
            }

            var networkEntity = entityManager.CreateEntity(GetNetworkEventComponents());
            entityManager.SetComponentData(networkEntity, new FromCharacter
            {
                Character = localCharacter,
                User = localUser
            });
            entityManager.SetComponentData(networkEntity, ChatNetworkEventType);
            entityManager.SetComponentData(networkEntity, new ChatMessageEvent
            {
                MessageText = new FixedString512Bytes(command),
                MessageType = ChatMessageType.Local,
                ReceiverEntity = entityManager.GetComponentData<NetworkId>(localUser)
            });

            _activeBossIndex = boss.Index;
            _forcedBossIndex = -1;
            if (!isForced)
            {
                AdvanceScheduledBoss(boss, isPreferred);
            }
            _requestSentAt = Time.unscaledTime;
            _loggedUnknownResponse = false;
            Plugin.Instance.Log.LogDebug($"Consulta interna enviada: {command}");
        }
        catch (Exception ex)
        {
            Plugin.Instance.Log.LogError($"Falha ao enviar consulta interna {command}: {ex}");
            _nextQueryAt = Time.unscaledTime + 5f;
        }
    }

    private void CompleteActiveRequest()
    {
        _activeBossIndex = -1;
        _loggedUnknownResponse = false;
        _nextQueryAt = Time.unscaledTime + GapBetweenBossQueriesSeconds;
    }

    private void TogglePinned(BossState boss)
    {
        boss.IsPinned = !boss.IsPinned;
        Plugin.PinnedBosses.Value = string.Join(',', _bosses
            .Where(item => item.IsPinned)
            .OrderBy(item => item.Index)
            .Select(item => item.CommandName));
        Plugin.Instance.Config.Save();
    }

    private void MarkBossKilled(BossState boss)
    {
        boss.HasResponse = true;
        boss.IsAlive = false;
        boss.HasError = false;
        boss.RemainingSeconds = 0f;
        _forcedBossIndex = boss.Index;

        if (ActiveBoss != null)
        {
            CompleteActiveRequest();
        }

        _nextQueryAt = Time.unscaledTime + 0.1f;
        Plugin.Instance.Log.LogDebug($"Boss marcado como morto manualmente na overlay: {boss.DisplayName}.");
    }

    private static bool TryApplyResponse(string rawText, BossState boss, out bool responseCompleted)
    {
        responseCompleted = false;
        var text = StripRichText(rawText);

        if (IsAvailableText(text))
        {
            boss.HasResponse = true;
            boss.IsAlive = true;
            boss.HasError = false;
            boss.RemainingSeconds = 0f;
            responseCompleted = true;
            return true;
        }

        if (IsNotFoundText(text))
        {
            boss.HasResponse = true;
            boss.IsAlive = false;
            boss.HasError = true;
            boss.RemainingSeconds = 0f;
            responseCompleted = true;
            return true;
        }

        var hasBossStatus =
            (text.Contains(boss.CommandName, StringComparison.OrdinalIgnoreCase) ||
             text.Contains(boss.DisplayName, StringComparison.OrdinalIgnoreCase)) &&
            IsDeadText(text);
        var hasRespawnTime = TryParseTime(text, out var seconds);
        if (!hasBossStatus && !hasRespawnTime)
        {
            return false;
        }

        boss.HasResponse = true;
        boss.IsAlive = false;
        boss.HasError = false;
        if (hasRespawnTime)
        {
            boss.RemainingSeconds = Mathf.Max(0f, seconds);
            responseCompleted = true;
        }

        return true;
    }

    private static bool LooksLikeBossResponse(string text, BossState boss)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(boss.CommandName, StringComparison.OrdinalIgnoreCase) ||
               text.Contains(boss.DisplayName, StringComparison.OrdinalIgnoreCase) ||
               IsNotFoundText(text) ||
               System.Text.RegularExpressions.Regex.IsMatch(
                   text,
                   "\\b(respawn|renasc|cooldown|tempo restante|tempo para)\\b",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsNotFoundText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"boss\s+n(?:\u00e3o|ao)\s+encontrado",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string StripRichText(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty).Trim();
    }

    private static bool IsDeadText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            "\\b(morto|morta|dead|destru[ií]do|destru[ií]da)\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool IsAvailableText(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            text,
            "\\b(dispon[ií]vel|available|alive|vivo|viva|renasceu|spawnado|liberado|up)\\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool TryParseTime(string text, out float seconds)
    {
        seconds = 0f;

        var clock = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?<!\d)(?<a>\d{1,3}):(?<b>\d{2})(?::(?<c>\d{2}))?(?!\d)");
        if (clock.Success)
        {
            var a = int.Parse(clock.Groups["a"].Value);
            var b = int.Parse(clock.Groups["b"].Value);
            var c = clock.Groups["c"].Success ? int.Parse(clock.Groups["c"].Value) : 0;
            seconds = clock.Groups["c"].Success ? a * 3600f + b * 60f + c : a * 60f + b;
            return true;
        }

        var unitMatches = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>d(?:ia|ias)?|h(?:ora|oras)?|m(?:in(?:uto|utos)?)?|s(?:eg(?:undo|undos)?)?)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in unitMatches)
        {
            var value = float.Parse(match.Groups["value"].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            seconds += unit.StartsWith("d") ? value * 86400f :
                        unit.StartsWith("h") ? value * 3600f :
                        unit.StartsWith("m") ? value * 60f : value;
        }

        return unitMatches.Count > 0;
    }

    private void OnGUI()
    {
        if (!Plugin.Enabled.Value)
        {
            return;
        }

        EnsureStyles();
        var scale = UiScale;
        var logicalScreenWidth = Screen.width / scale;
        var logicalScreenHeight = Screen.height / scale;
        var width = Mathf.Max(420f, Plugin.PanelWidth.Value);
        var height = Mathf.Clamp(Plugin.PanelHeight.Value, 220f, Mathf.Max(220f, logicalScreenHeight - 12f));
        const float toggleSize = 34f;
        const float gap = 6f;
        const float headerHeight = 34f;

        InitializePanelPosition(width, height, toggleSize, gap, logicalScreenWidth);
        ClampPanelPosition(width, height, toggleSize, gap, logicalScreenWidth, logicalScreenHeight);

        var panelRect = new Rect(_panelPosition.x, _panelPosition.y, width, height);
        HandlePanelDragging(panelRect, headerHeight);
        ClampPanelPosition(width, height, toggleSize, gap, logicalScreenWidth, logicalScreenHeight);

        var toggleRect = new Rect(panelRect.x + width + gap, panelRect.y, toggleSize, toggleSize);
        var resizeRect = new Rect(toggleRect.x + 1f, toggleRect.y + toggleSize + 4f, toggleSize - 2f, 20f);
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        if (GUI.Button(toggleRect, _overlayShown ? "●" : "○", _toggleStyle))
        {
            _overlayShown = !_overlayShown;
        }

        if (GUI.Button(resizeRect, $"{Mathf.RoundToInt(scale * 100f)}%", _resizeStyle))
        {
            CycleUiScale();
        }

        if (!_overlayShown)
        {
            GUI.matrix = previousMatrix;
            return;
        }

        GUI.Box(panelRect, GUIContent.none, _boxStyle);
        GUI.Label(
            new Rect(panelRect.x + panelRect.width - 72f, panelRect.y + 6f, 60f, headerHeight - 8f),
            _bosses.Count.ToString(),
            _labelStyle);
        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + 6f, panelRect.width - 92f, headerHeight - 8f),
            "<b>Bosses</b>  (arraste o cabeçalho)",
            _labelStyle);

        var viewport = new Rect(
            panelRect.x + 8f,
            panelRect.y + headerHeight,
            panelRect.width - 16f,
            panelRect.height - headerHeight - 8f);
        var rowHeight = Mathf.Max(22f, OverlayFontSize + 8f);
        const float sectionHeaderHeight = 30f;
        var pinnedBosses = _bosses
            .Where(boss => boss.IsPinned)
            .OrderBy(boss => boss.Index)
            .ToList();
        var actBosses = Enumerable.Range(1, 4)
            .Select(GetActBosses)
            .ToList();
        var contentHeight = 8f;
        if (pinnedBosses.Count > 0)
        {
            contentHeight += sectionHeaderHeight + pinnedBosses.Count * rowHeight;
        }

        for (var act = 1; act <= 4; act++)
        {
            contentHeight += sectionHeaderHeight;
            if (_expandedActs[act - 1])
            {
                contentHeight += actBosses[act - 1].Count * rowHeight;
            }
        }

        contentHeight = Mathf.Max(viewport.height, contentHeight);
        var maxScrollY = Mathf.Max(0f, contentHeight - viewport.height);
        if (Event.current != null && Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition))
        {
            _scrollPosition.y = Mathf.Clamp(_scrollPosition.y + Event.current.delta.y * rowHeight, 0f, maxScrollY);
            Event.current.Use();
        }
        _scrollPosition.y = Mathf.Clamp(_scrollPosition.y, 0f, maxScrollY);

        // O overload de BeginScrollView usado pelo UnityEngine.GUI não é
        // suportado pelo wrapper IL2CPP desta versão. O grupo fornece o mesmo
        // recorte e a rolagem é aplicada manualmente pelo mouse wheel.
        GUI.BeginGroup(viewport);
        var rowWidth = viewport.width - 10f;
        var killButtonWidth = Mathf.Clamp(OverlayFontSize * 3.8f, 62f, 76f);
        var pinButtonWidth = Mathf.Clamp(OverlayFontSize * 2.8f, 48f, 60f);
        var cursorY = 4f - _scrollPosition.y;

        if (pinnedBosses.Count > 0)
        {
            GUI.Label(new Rect(4f, cursorY, rowWidth, sectionHeaderHeight), $"Preferenciais ({pinnedBosses.Count})", _sectionStyle);
            cursorY += sectionHeaderHeight;
            foreach (var boss in pinnedBosses)
            {
                DrawBossRow(boss, cursorY, rowWidth, rowHeight, killButtonWidth, pinButtonWidth);
                cursorY += rowHeight;
            }
        }

        for (var act = 1; act <= 4; act++)
        {
            var actBossList = actBosses[act - 1];
            var actHeader = $"{(_expandedActs[act - 1] ? "[-]" : "[+]" )} {GetActTitle(act)} ({actBossList.Count})";
            if (GUI.Button(new Rect(4f, cursorY, rowWidth, sectionHeaderHeight - 2f), actHeader, _sectionStyle))
            {
                ToggleAct(act);
            }

            cursorY += sectionHeaderHeight;
            if (!_expandedActs[act - 1])
            {
                continue;
            }

            foreach (var boss in actBossList)
            {
                DrawBossRow(boss, cursorY, rowWidth, rowHeight, killButtonWidth, pinButtonWidth);
                cursorY += rowHeight;
            }
        }
        GUI.EndGroup();
        GUI.matrix = previousMatrix;
    }

    private void InitializePanelPosition(float width, float height, float toggleSize, float gap, float logicalScreenWidth)
    {
        if (_panelPositionInitialized)
        {
            return;
        }

        var defaultX = logicalScreenWidth - width - toggleSize - gap - Plugin.RightOffset.Value;
        var defaultY = Plugin.TopOffset.Value;
        _panelPosition = new Vector2(
            Plugin.PositionX.Value >= 0f ? Plugin.PositionX.Value : defaultX,
            Plugin.PositionY.Value >= 0f ? Plugin.PositionY.Value : defaultY);
        _panelPositionInitialized = true;
    }

    private void ClampPanelPosition(float width, float height, float toggleSize, float gap, float logicalScreenWidth, float logicalScreenHeight)
    {
        var maxX = Mathf.Max(4f, logicalScreenWidth - width - toggleSize - gap - 4f);
        var maxY = Mathf.Max(4f, logicalScreenHeight - 38f);
        _panelPosition.x = Mathf.Clamp(_panelPosition.x, 4f, maxX);
        _panelPosition.y = Mathf.Clamp(_panelPosition.y, 4f, maxY);
    }

    private void CycleUiScale()
    {
        var current = UiScale;
        var next = UiScalePresets.FirstOrDefault(value => value > current + 0.001f);
        if (next <= 0f)
        {
            next = UiScalePresets[0];
        }

        Plugin.UiScale.Value = next;
        Plugin.Instance.Config.Save();
    }

    private void HandlePanelDragging(Rect panelRect, float headerHeight)
    {
        var currentEvent = Event.current;
        if (currentEvent == null || currentEvent.button != 0)
        {
            return;
        }

        // O painel e desenhado com GUI.matrix. O mouse continua chegando em
        // pixels da tela, enquanto panelRect usa coordenadas logicas; sem a
        // conversao o arraste fica inconsistente em escalas diferentes de 100%.
        var logicalMousePosition = currentEvent.mousePosition / UiScale;

        if (currentEvent.type == EventType.MouseDown &&
            new Rect(panelRect.x, panelRect.y, panelRect.width, headerHeight).Contains(logicalMousePosition))
        {
            _draggingPanel = true;
            _dragOffset = logicalMousePosition - _panelPosition;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && _draggingPanel)
        {
            _panelPosition = logicalMousePosition - _dragOffset;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && _draggingPanel)
        {
            _draggingPanel = false;
            Plugin.PositionX.Value = _panelPosition.x;
            Plugin.PositionY.Value = _panelPosition.y;
            Plugin.Instance.Config.Save();
            currentEvent.Use();
        }
    }

    private string GetBossStatusText(BossState boss)
    {
        if (!boss.HasResponse)
        {
            return ActiveBoss == boss ? "consultando..." : "aguardando...";
        }

        if (boss.IsAlive)
        {
            return "VIVO";
        }

        if (boss.HasError)
        {
            return "NAO ENCONTRADO";
        }

        if (boss.RemainingSeconds > 0f)
        {
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(boss.RemainingSeconds));
            return remaining.TotalDays >= 1
                ? $"{(int)remaining.TotalDays}d {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        return "MORTO";
    }

    private void EnsureStyles()
    {
        if (_boxStyle != null && _labelStyle != null && _toggleStyle != null && _killButtonStyle != null && _pinButtonStyle != null && _sectionStyle != null && _resizeStyle != null)
        {
            _labelStyle.fontSize = OverlayFontSize;
            _toggleStyle.fontSize = OverlayFontSize + 4;
            _killButtonStyle.fontSize = Mathf.Max(10, OverlayFontSize - 2);
            _pinButtonStyle.fontSize = Mathf.Max(10, OverlayFontSize - 2);
            _sectionStyle.fontSize = OverlayFontSize;
            _resizeStyle.fontSize = Mathf.Max(9, OverlayFontSize - 5);
            return;
        }

        _boxStyle = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
        };
        _labelStyle = new GUIStyle
        {
            alignment = TextAnchor.UpperLeft,
            richText = true,
            fontSize = OverlayFontSize,
            clipping = TextClipping.Clip,
            normal = { textColor = Color.white }
        };
        _toggleStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = OverlayFontSize + 4,
            normal = { textColor = new Color(0.35f, 0.85f, 1f, 0.95f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.2f, 0.65f, 0.85f, 1f) }
        };
        _killButtonStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(10, OverlayFontSize - 2),
            normal = { textColor = new Color(1f, 0.55f, 0.55f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(1f, 0.3f, 0.3f, 1f) }
        };
        _pinButtonStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(10, OverlayFontSize - 2),
            normal = { textColor = new Color(0.65f, 0.8f, 1f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.35f, 0.6f, 1f, 1f) }
        };
        _sectionStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = OverlayFontSize,
            normal = { textColor = new Color(0.95f, 0.85f, 0.45f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(1f, 0.95f, 0.55f, 1f) }
        };
        _resizeStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Max(9, OverlayFontSize - 5),
            normal = { textColor = new Color(0.65f, 0.72f, 0.8f, 0.85f) },
            hover = { textColor = Color.white },
            active = { textColor = new Color(0.45f, 0.8f, 1f, 1f) }
        };
    }
}

[HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
internal static class ClientChatSystemPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ClientChatSystem __instance)
    {
        BossRespawnOverlayBehaviour.Instance?.HandleChatUpdate(__instance);
    }
}

[HarmonyPatch(typeof(ClientChatSystem), "ParseCommand")]
internal static class ClientChatCommandPatch
{
    [HarmonyPrefix]
    private static void Prefix(string text)
    {
        BossRespawnOverlayBehaviour.Instance?.NotifyManualChatCommand(text);
    }
}
