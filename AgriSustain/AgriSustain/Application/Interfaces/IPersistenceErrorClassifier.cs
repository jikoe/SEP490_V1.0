namespace AgriSustain.Application.Interfaces;

public interface IPersistenceErrorClassifier
{
    bool IsConflict(Exception exception);
}
