using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace UserDepartmentPredictionApp
{
    

    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }  // Define Users table

        // Configuration for SQLite Database
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=users.db");
        }
    }


}
