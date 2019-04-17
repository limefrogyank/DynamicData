using DynamicData.SignalR.TestModel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicData.SignalR.TestWebApp.Data
{
    public class TestContext : DbContext
    {
       

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlite("Filename=SqliteDatabase.db");
        }
        public DbSet<Person> Persons { get; set; }
    }
}
