using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace EmbeddingPoC;

public class TextEmbedding
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmbeddingDbContext : DbContext
{
    public EmbeddingDbContext(DbContextOptions<EmbeddingDbContext> options) : base(options)
    {
    }

    public DbSet<TextEmbedding> TextEmbeddings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        
        modelBuilder.Entity<TextEmbedding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Embedding).HasColumnType("vector(384)"); // all-minilm produces 384-dimensional vectors
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // Add index for vector similarity search
            entity.HasIndex(e => e.Embedding)
                .HasMethod("ivfflat")
                .HasOperators("vector_cosine_ops");
        });
    }
}