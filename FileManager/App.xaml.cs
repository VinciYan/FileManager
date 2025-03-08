using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using FileManager.Data;
using FileManager.ViewModels;
using FileManager.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace FileManager
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 注册数据库上下文
            services.AddDbContext<FileManagerDbContext>();
            
            // 注册视图模型
            services.AddTransient<MainViewModel>();
            
            // 注册主窗口
            services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 获取数据库上下文并确保数据库被创建
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
                //dbContext.Database.EnsureDeleted(); // 删除现有数据库
                //dbContext.Database.EnsureCreated(); // 创建新数据库
                dbContext.Database.Migrate(); // 应用所有待处理的迁移并创建数据库
            }

            // 显示主窗口
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _serviceProvider?.Dispose();
        }
    }
} 