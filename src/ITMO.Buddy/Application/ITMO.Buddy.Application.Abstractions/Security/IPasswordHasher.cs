namespace ITMO.Buddy.Application.Abstractions.Security;

/// <summary>
/// Абстракция безопасного хеширования и проверки паролей.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Создаёт необратимый хеш пароля.
    /// Реализация должна использовать адаптивный
    /// алгоритм хеширования паролей.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Проверяет пароль относительно сохранённого хеша.
    /// </summary>
    bool Verify(
        string password,
        string passwordHash);
}