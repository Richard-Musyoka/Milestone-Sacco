using Microsoft.EntityFrameworkCore;
using SaccoManagementSystem.Models;  
namespace SaccoManagementSystem.Data;
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }  
    }

