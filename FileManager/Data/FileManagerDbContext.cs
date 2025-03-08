using Microsoft.EntityFrameworkCore;
using FileManager.Models;

namespace FileManager.Data
{
    public class FileManagerDbContext : DbContext
    {
        public DbSet<FileItem> FileItems { get; set; }
        public DbSet<UploadLog> UploadLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=FileManager.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FileItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Path).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<UploadLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LocalPath).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Operation).IsRequired().HasColumnType("INTEGER");
                entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => e.Status);
            });
        }
    }
} 