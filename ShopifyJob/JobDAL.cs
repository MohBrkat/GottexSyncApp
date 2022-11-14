using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyncAppEntities.Models.EF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncAppJob
{
    public class JobDAL
    {
        private ShopifyAppContext _context;
        private IConfiguration _configuration;
        public JobDAL(IConfiguration configuration, ShopifyAppContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public void InitilizeDAL()
        {
            string connectionString = _configuration.GetConnectionString("DbConnection");

            var services = new ServiceCollection();
            services.AddDbContext<ShopifyAppContext>(options => options.UseSqlServer(connectionString));
            var serviceProvider = services.BuildServiceProvider();

            _context = serviceProvider.GetService<ShopifyAppContext>();
        }

        public List<Configrations> GetConfigurations()
        {
            return _context.Configrations.ToList();
        }
    }
}
