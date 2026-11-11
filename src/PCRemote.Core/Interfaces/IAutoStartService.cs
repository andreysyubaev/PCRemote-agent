namespace PCRemote.Core.Interfaces;

public interface IAutoStartService
{
    // проверяет зарегистрировано ли приложение в автозапуске
    bool IsEnabled();

    // добавляет приложение в автозапуск
    void Enable();

    // удаляет приложение из автозапуска
    void Disable();
}