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

    private ServiceRegistry()
    {
        _loggerService = new Lazy<ILoggerService>(
            () => new LoggerService(_loggerConfiguration.Value));

        _presetPersistence = new Lazy<IPresetPersistenceService>(
            () => new PresetPersistenceService(_loggerService.Value, _mainDiskRepository.Value, _backupStorageManager.Value));

        _presetMigration = new Lazy<IPresetMigrationService>(
            () => new PresetMigrationService(_presetPersistence.Value));
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
}