using ComputerEquipment.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ComputerEquipment.Api.Data;

/// <summary>
/// Контекст EF Core для PostgreSQL: словарь типов и записи о технике.
/// </summary>
/// <param name="options">Параметры контекста (строка подключения, провайдер).</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Имя уникального индекса по учетному номеру; по нему распознается дубль номера.</summary>
    public const string InventoryNumberIndex = "ix_equipment_inventory_number";

    /// <summary>Имя уникального индекса по названию типа.</summary>
    public const string TypeNameIndex = "ix_equipment_types_name";

    /// <summary>Записи о технике.</summary>
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();

    /// <summary>Словарь типов техники.</summary>
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentType>(e =>
        {
            e.ToTable("equipment_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(EquipmentRules.TypeNameMaxLength).IsRequired();
            e.HasIndex(x => x.Name).IsUnique().HasDatabaseName(TypeNameIndex);
        });

        modelBuilder.Entity<EquipmentItem>(e =>
        {
            // Ограничения продублированы в БД: данные корректны, даже если обойти API.
            e.ToTable("equipment", t =>
            {
                t.HasCheckConstraint("ck_equipment_room", "\"Room\" BETWEEN 1 AND 1000");
                t.HasCheckConstraint("ck_equipment_inventory_number", "\"InventoryNumber\" ~ '^[0-9A-Z]{1,32}$'");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.InventoryNumber).HasMaxLength(EquipmentRules.InventoryNumberMaxLength).IsRequired();
            e.Property(x => x.Name).HasMaxLength(EquipmentRules.NameMaxLength).IsRequired();

            // Уникальный индекс — главная защита от одновременного ввода одной и той же записи.
            e.HasIndex(x => x.InventoryNumber).IsUnique().HasDatabaseName(InventoryNumberIndex);
            e.HasIndex(x => x.TypeId);

            // uint + IsRowVersion в Npgsql отображается на системный столбец xmin.
            e.Property(x => x.Version).IsRowVersion();

            e.HasOne(x => x.Type)
                .WithMany(t => t.Items)
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
