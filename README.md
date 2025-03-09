# FileManager

基于 WPF 和 MinIO 的现代文件管理系统，提供强大的文件去重和管理功能。

## 🌟 特性

- 📁 智能文件去重：使用MD5哈希自动检测和处理重复文件
- 🔄 实时同步：本地文件系统与MinIO对象存储的无缝集成
- 🎯 拖拽上传：支持文件和文件夹的拖拽上传
- 📊 任务面板：实时显示上传、删除等操作的进度和状态
- 🔍 智能搜索：支持按文件名、备注和MinIO链接搜索
- 📝 元数据管理：支持为文件添加备注等元数据
- 🌲 层级结构：支持文件夹的多层级组织
- 🔁 文件操作：支持复制、剪切、粘贴、重命名等基本操作

## 🚀 技术栈

- 框架：WPF (.NET Core)
- 存储：MinIO 对象存储
- 数据库：SQLite + Entity Framework Core（本地数据存储）
- UI：Modern WPF UI

## 📦 安装

1. 克隆仓库：
```bash
git clone https://github.com/yourusername/FileManager.git
```

2. 配置MinIO：
   - 在项目根目录创建 `appsettings.json` 文件
   - 添加以下配置（替换为您的MinIO配置）：
```json
{
  "MinioConfig": {
    "Endpoint": "your-minio-endpoint",
    "AccessKey": "your-minio-endpoint",
    "SecretKey": "your-secret-key",
    "BucketName": "your-bucket-name",
    "Secure": false
  }
} 
```

3. 运行数据库迁移：
```bash
dotnet ef database update
```
这将创建本地SQLite数据库文件并应用所有迁移。

4. 编译并运行项目：
```bash
dotnet build
dotnet run
```

## 💡 使用说明

1. **文件上传**
   - 直接将文件或文件夹拖拽到应用窗口
   - 系统自动计算MD5并处理重复文件
   - 上传进度实时显示在任务面板
   - 文件元数据保存在本地SQLite数据库中

2. **文件管理**
   - 双击文件夹进行导航
   - 使用右键菜单进行复制、剪切、粘贴等操作
   - 支持批量操作
   - 所有操作历史记录在本地数据库中

3. **搜索功能**
   - 使用顶部搜索框搜索文件
   - 支持按文件名、备注、MinIO链接搜索
   - 基于SQLite的快速本地搜索

4. **查看模式**
   - 支持列表和详细信息两种查看模式
   - 可以自定义显示列

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 🙏 致谢

感谢以下开源项目：
- [MinIO](https://min.io/) - 分布式对象存储系统
- [.NET Core](https://dotnet.microsoft.com/) - 跨平台的开发框架
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) - 现代化的 ORM 框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 工具包
- [SQLite](https://www.sqlite.org/) - 轻量级数据库引擎