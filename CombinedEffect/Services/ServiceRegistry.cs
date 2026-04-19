using CombinedEffect.Services.Interfaces;

namespace CombinedEffect.Services;

internal sealed class ServiceRegistry
{
    private static readonly Lazy<ServiceRegistry> _instance = new(() => new ServiceRegistry());
    public static ServiceRegistry Instance => _instance.Value;

    private readonly Lazy<IEffectSerializationService> _effectSerialization =
        new(() => new EffectSerializationService());

    private readonly Lazy<ILoggerConfiguration> _loggerConfiguration =
        new(() => new LoggerConfiguration());

    private readonly Lazy<ILoggerService> _loggerService;

    private readonly Lazy<IMainDiskRepository> _mainDiskRepository =
        new(() => new MainDiskRepository());

    private readonly Lazy<IBackupStorageManager> _backupStorageManager =
        new(() => new BackupStorageManager());

    private readonly Lazy<IPresetPersistenceService> _presetPersistence;

    private readonly Lazy<IPresetMigrationService> _presetMigration;

    private readonly Lazy<IWindowThemeService> _windowTheme =
        new(() => new WindowThemeService());

    private readonly Lazy<IRecentPresetService> _recentPreset =
        new(() => new RecentPresetService());

    private readonly Lazy<IUISettingsService> _uiSettings =
        new(() => new UISettingsService());

    private readonly Lazy<IPresetExchangeService> _presetExchange =
        new(() => new PresetExchangeService());

    private readonly Lazy<IPresetExchangeDialogService> _presetExchangeDialog =
        new(() => new PresetExchangeDialogService());

    private readonly Lazy<IPresetApplyPlannerService> _presetApplyPlanner;

    private readonly Lazy<IUserDialogService> _userDialog =
        new(() => new UserDialogService());

    private readonly Lazy<IHistoryWindowService> _historyWindow =
        new(() => new HistoryWindowService());

    private readonly Lazy<IResilienceService> _resilience;

    private ServiceRegistry()
    {
        _loggerService = new Lazy<ILoggerService>(
            () => new LoggerService(_loggerConfiguration.Value));

        _resilience = new Lazy<IResilienceService>(
            () => new ResilienceService(_loggerService.Value));

        _presetPersistence = new Lazy<IPresetPersistenceService>(
            () => new PresetPersistenceService(_loggerService.Value, _mainDiskRepository.Value, _backupStorageManager.Value));

        _presetMigration = new Lazy<IPresetMigrationService>(
            () => new PresetMigrationService(_presetPersistence.Value));

        _presetApplyPlanner = new Lazy<IPresetApplyPlannerService>(
            () => new PresetApplyPlannerService(_effectSerialization.Value));
    }

    private readonly Lazy<IHistoryRepository> _historyRepository =
        new(() => new HistoryRepository());

    public IEffectSerializationService EffectSerialization => _effectSerialization.Value;
    public IPresetPersistenceService PresetPersistence => _presetPersistence.Value;
    public IPresetMigrationService PresetMigration => _presetMigration.Value;
    public IWindowThemeService WindowTheme => _windowTheme.Value;
    public IRecentPresetService RecentPreset => _recentPreset.Value;
    public IUISettingsService UISettings => _uiSettings.Value;
    public IHistoryRepository HistoryRepository => _historyRepository.Value;
    public IPresetExchangeService PresetExchange => _presetExchange.Value;
    public IPresetExchangeDialogService PresetExchangeDialog => _presetExchangeDialog.Value;
    public IPresetApplyPlannerService PresetApplyPlanner => _presetApplyPlanner.Value;
    public IUserDialogService UserDialog => _userDialog.Value;
    public IHistoryWindowService HistoryWindow => _historyWindow.Value;
    public ILoggerService Logger => _loggerService.Value;
    public IResilienceService Resilience => _resilience.Value;
}