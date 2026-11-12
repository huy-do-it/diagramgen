using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Identity.Repositories.Data;

#nullable disable

namespace Identity.Repositories.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20261112000000_InitIdentity")]
partial class InitIdentity
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.10")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("Identity.Repositories.Entities.InboxMessage", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<Guid>("EventId")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("ProcessedAt")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("ReceivedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("Stream")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.HasKey("Id");

                b.HasIndex("EventId", "Stream")
                    .IsUnique();

                b.ToTable("InboxMessages", (string)null);
            });

        modelBuilder.Entity("Identity.Repositories.Entities.OutboxMessage", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<int>("Attempts")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("int")
                    .HasDefaultValue(0);

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("Envelope")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("LastError")
                    .HasColumnType("nvarchar(max)");

                b.Property<DateTime?>("PublishedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("Stream")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<string>("Type")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.HasKey("Id");

                b.HasIndex("PublishedAt");

                b.ToTable("OutboxMessages", (string)null);
            });

        modelBuilder.Entity("Identity.Repositories.Entities.RefreshToken", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.Property<DateTime>("ExpiresAt")
                    .HasColumnType("datetime2");

                b.Property<Guid?>("ReplacedById")
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime?>("RevokedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("TokenHash")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("nvarchar(128)");

                b.Property<Guid>("UserId")
                    .HasColumnType("uniqueidentifier");

                b.HasKey("Id");

                b.HasIndex("TokenHash")
                    .IsUnique();

                b.HasIndex("UserId");

                b.ToTable("RefreshTokens", (string)null);
            });

        modelBuilder.Entity("Identity.Repositories.Entities.User", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uniqueidentifier");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime2");

                b.Property<string>("FullName")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.Property<bool>("IsLocked")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bit")
                    .HasDefaultValue(false);

                b.Property<string>("PasswordHash")
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Role")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("nvarchar(20)");

                b.Property<string>("Username")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.HasKey("Id");

                b.HasIndex("Username")
                    .IsUnique();

                b.ToTable("Users", (string)null);
            });

        modelBuilder.Entity("Identity.Repositories.Entities.RefreshToken", b =>
            {
                b.HasOne("Identity.Repositories.Entities.User", "User")
                    .WithMany("RefreshTokens")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("User");
            });

        modelBuilder.Entity("Identity.Repositories.Entities.User", b =>
            {
                b.Navigation("RefreshTokens");
            });
#pragma warning restore 612, 618
    }
}
