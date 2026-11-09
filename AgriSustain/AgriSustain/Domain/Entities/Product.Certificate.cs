namespace AgriSustain.Domain.Entities;

public partial class Product
{
    public long? FarmCertificateId { get; set; }

    public virtual FarmCertificate? FarmCertificate { get; set; }
}
