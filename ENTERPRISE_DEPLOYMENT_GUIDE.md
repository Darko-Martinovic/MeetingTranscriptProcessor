# Enterprise Deployment Guide - Meeting Transcript Processor

## 🏢 Overview

This guide provides comprehensive instructions for deploying the Meeting Transcript Processor in enterprise environments, including security considerations, scalability requirements, and integration best practices.

## 📋 Prerequisites

### System Requirements

#### **Minimum Requirements**
- **OS**: Windows Server 2019+, Ubuntu 20.04+, or RHEL 8+
- **CPU**: 4 cores, 2.4GHz
- **RAM**: 8GB minimum, 16GB recommended
- **Storage**: 100GB available space
- **Network**: 1Gbps connection

#### **Recommended Production Environment**
- **OS**: Ubuntu 22.04 LTS or Windows Server 2022
- **CPU**: 8 cores, 3.0GHz+ 
- **RAM**: 32GB
- **Storage**: 500GB SSD with backup strategy
- **Network**: 10Gbps connection with redundancy

### Software Dependencies
- **.NET 8.0 Runtime**
- **Node.js 18+ LTS**
- **Reverse Proxy** (nginx, IIS, or Azure Application Gateway)
- **Database** (SQL Server, PostgreSQL, or SQLite for smaller deployments)

### Optional Components
- **Azure OpenAI** account for advanced AI features
- **Jira** instance with API access
- **Container Runtime** (Docker/Podman for containerized deployment)
- **Load Balancer** for high-availability setups

## 🚀 Deployment Options

### Option 1: Traditional Server Deployment

#### **Step 1: Environment Setup**
```bash
# Ubuntu/Debian
sudo apt update && sudo apt install -y dotnet-runtime-8.0 nodejs npm nginx

# RHEL/CentOS
sudo dnf install -y dotnet-runtime-8.0 nodejs npm nginx

# Windows (PowerShell as Administrator)
# Install via Windows Package Manager or download from Microsoft
```

#### **Step 2: Application Deployment**
```bash
# Create application directory
sudo mkdir -p /opt/meeting-transcript-processor
cd /opt/meeting-transcript-processor

# Extract application files
sudo tar -xzf meeting-transcript-processor-v1.0.tar.gz

# Set permissions
sudo chown -R meeting-app:meeting-app /opt/meeting-transcript-processor
sudo chmod +x /opt/meeting-transcript-processor/MeetingTranscriptProcessor

# Install frontend dependencies
cd frontend/meeting-transcript-ui
npm install --production
npm run build
```

#### **Step 3: Configuration**
```bash
# Copy environment template
sudo cp .env.example .env

# Edit configuration
sudo nano .env
```

**Essential Configuration:**
```env
# Application Settings
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5000

# File Processing
INCOMING_DIRECTORY=/opt/meeting-data/incoming
PROCESSING_DIRECTORY=/opt/meeting-data/processing
ARCHIVE_DIRECTORY=/opt/meeting-data/archive

# Security (Optional but recommended)
AOAI_ENDPOINT=https://your-company.openai.azure.com
AOAI_APIKEY=[Your-Secure-API-Key]
JIRA_URL=https://your-company.atlassian.net
JIRA_API_TOKEN=[Your-Secure-API-Token]
JIRA_EMAIL=service-account@your-company.com

# Performance Tuning
MAX_CONCURRENT_FILES=5
ENABLE_VALIDATION=true
```

#### **Step 4: Service Configuration**
```bash
# Create systemd service (Linux)
sudo tee /etc/systemd/system/meeting-transcript-processor.service > /dev/null <<EOF
[Unit]
Description=Meeting Transcript Processor
After=network.target

[Service]
Type=notify
ExecStart=/opt/meeting-transcript-processor/MeetingTranscriptProcessor --web
Restart=always
User=meeting-app
Group=meeting-app
WorkingDirectory=/opt/meeting-transcript-processor
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
sudo systemctl enable meeting-transcript-processor
sudo systemctl start meeting-transcript-processor
```

### Option 2: Docker Containerized Deployment

#### **Step 1: Create Dockerfile**
```dockerfile
# Backend Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MeetingTranscriptProcessor.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MeetingTranscriptProcessor.dll", "--web"]
```

#### **Step 2: Docker Compose Configuration**
```yaml
version: '3.8'
services:
  meeting-processor-backend:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    volumes:
      - ./data:/app/data
      - ./config:/app/config
    restart: unless-stopped
    
  meeting-processor-frontend:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./frontend/dist:/usr/share/nginx/html
      - ./nginx.conf:/etc/nginx/nginx.conf
    depends_on:
      - meeting-processor-backend
    restart: unless-stopped
```

#### **Step 3: Deploy with Docker Compose**
```bash
# Build and start containers
docker-compose up -d

# View logs
docker-compose logs -f

# Scale backend if needed
docker-compose up -d --scale meeting-processor-backend=3
```

### Option 3: Cloud Deployment (Azure)

#### **Azure App Service + Container Instances**
```bash
# Create resource group
az group create --name meeting-processor-rg --location eastus

# Create App Service plan
az appservice plan create --name meeting-processor-plan --resource-group meeting-processor-rg --sku B2 --is-linux

# Create web app
az webapp create --resource-group meeting-processor-rg --plan meeting-processor-plan --name meeting-processor-app --deployment-container-image-name your-registry/meeting-processor:latest

# Configure environment variables
az webapp config appsettings set --resource-group meeting-processor-rg --name meeting-processor-app --settings ASPNETCORE_ENVIRONMENT=Production
```

## 🔒 Security Configuration

### SSL/TLS Setup

#### **nginx Configuration**
```nginx
server {
    listen 443 ssl;
    server_name meeting-processor.your-company.com;
    
    ssl_certificate /etc/ssl/certs/meeting-processor.crt;
    ssl_certificate_key /etc/ssl/private/meeting-processor.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    
    # File upload size limit
    client_max_body_size 100M;
}

# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name meeting-processor.your-company.com;
    return 301 https://$server_name$request_uri;
}
```

### Firewall Configuration
```bash
# Ubuntu UFW
sudo ufw allow 22/tcp      # SSH
sudo ufw allow 80/tcp      # HTTP
sudo ufw allow 443/tcp     # HTTPS
sudo ufw enable

# RHEL/CentOS firewalld  
sudo firewall-cmd --permanent --add-service=ssh
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload
```

### Authentication & Authorization

#### **Azure Active Directory Integration (Future)**
```csharp
// Program.cs additions for AAD
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireValidUser", policy =>
        policy.RequireAuthenticatedUser());
});
```

## 📈 Scalability & Performance

### High Availability Setup

#### **Load Balancer Configuration**
```yaml
# HAProxy configuration
global
    daemon
    
defaults
    mode http
    timeout connect 5000ms
    timeout client 50000ms
    timeout server 50000ms
    
frontend meeting_processor_frontend
    bind *:80
    bind *:443 ssl crt /etc/ssl/certs/meeting-processor.pem
    redirect scheme https if !{ ssl_fc }
    default_backend meeting_processor_backend
    
backend meeting_processor_backend
    balance roundrobin
    server app1 10.0.1.10:5000 check
    server app2 10.0.1.11:5000 check
    server app3 10.0.1.12:5000 check
```

### Database Configuration

#### **PostgreSQL Setup for Enterprise**
```sql
-- Create dedicated database
CREATE DATABASE meeting_transcripts;
CREATE USER meeting_app WITH ENCRYPTED PASSWORD 'secure_password';
GRANT ALL PRIVILEGES ON DATABASE meeting_transcripts TO meeting_app;

-- Connection string in appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=meeting_transcripts;Username=meeting_app;Password=secure_password"
  }
}
```

### Performance Tuning

#### **Application Settings**
```env
# Increase concurrent processing for high-volume environments
MAX_CONCURRENT_FILES=10

# Optimize AI processing
AI_MAX_TOKENS=2000
AI_TEMPERATURE=0.1

# Enable advanced caching
ENABLE_CACHING=true
CACHE_DURATION_MINUTES=60
```

#### **System Optimization**
```bash
# Increase file descriptor limits
echo "meeting-app soft nofile 65536" >> /etc/security/limits.conf
echo "meeting-app hard nofile 65536" >> /etc/security/limits.conf

# Optimize network settings
echo "net.core.somaxconn = 65536" >> /etc/sysctl.conf
sysctl -p
```

## 📊 Monitoring & Maintenance

### Application Monitoring
```bash
# Health check endpoint
curl -f http://localhost:5000/api/status || exit 1

# Log monitoring with rsyslog
echo "local0.*    /var/log/meeting-processor.log" >> /etc/rsyslog.conf
systemctl restart rsyslog
```

### Backup Strategy
```bash
#!/bin/bash
# Backup script - run daily via cron

BACKUP_DIR="/backup/meeting-processor/$(date +%Y%m%d)"
mkdir -p $BACKUP_DIR

# Backup application data
tar -czf $BACKUP_DIR/data.tar.gz /opt/meeting-data/

# Backup configuration
cp /opt/meeting-transcript-processor/.env $BACKUP_DIR/

# Backup database (if using PostgreSQL)
pg_dump meeting_transcripts > $BACKUP_DIR/database.sql

# Retain last 30 days
find /backup/meeting-processor/ -type d -mtime +30 -exec rm -rf {} \;
```

### Update Procedures
```bash
#!/bin/bash
# Zero-downtime update script

# Download new version
wget https://releases.meeting-processor.com/v1.1.0.tar.gz

# Stop service
sudo systemctl stop meeting-transcript-processor

# Backup current version
sudo cp -r /opt/meeting-transcript-processor /opt/meeting-transcript-processor.backup

# Deploy new version
sudo tar -xzf v1.1.0.tar.gz -C /opt/meeting-transcript-processor --strip-components=1

# Start service
sudo systemctl start meeting-transcript-processor

# Verify health
sleep 10
curl -f http://localhost:5000/api/status && echo "Update successful" || echo "Update failed"
```

## 🔧 Troubleshooting

### Common Issues

#### **Service Won't Start**
```bash
# Check service status
sudo systemctl status meeting-transcript-processor

# View logs
sudo journalctl -u meeting-transcript-processor -f

# Common fixes
sudo systemctl daemon-reload
sudo systemctl restart meeting-transcript-processor
```

#### **High Memory Usage**
```bash
# Monitor memory usage
ps aux | grep MeetingTranscriptProcessor

# Adjust .NET GC settings
export DOTNET_GCHeapHardLimit=2G
export DOTNET_GCHeapHardLimitPercent=50
```

#### **File Processing Issues**
```bash
# Check directory permissions
ls -la /opt/meeting-data/

# Fix permissions
sudo chown -R meeting-app:meeting-app /opt/meeting-data/
sudo chmod -R 755 /opt/meeting-data/
```

### Performance Monitoring
```bash
# CPU and memory monitoring
top -p $(pgrep -f MeetingTranscriptProcessor)

# Network monitoring
netstat -an | grep :5000

# Disk I/O monitoring
iotop -u meeting-app
```

## 📞 Enterprise Support

### Support Channels
- **Email**: enterprise-support@meeting-processor.com
- **Phone**: 1-800-MEETING (24/7 for Enterprise customers)
- **Dedicated Slack**: Private channel for Enterprise customers
- **SLA**: 99.9% uptime guarantee with 4-hour response time

### Professional Services
- **Implementation Consulting**: $5,000 - $15,000
- **Custom Integration Development**: $10,000 - $50,000
- **Training Programs**: $2,500 per session
- **24/7 Managed Services**: $2,000/month

### Compliance & Certifications
- **SOC 2 Type II**: In progress (Q2 2025)
- **ISO 27001**: Planned for Q3 2025
- **GDPR Compliance**: Built-in data privacy controls
- **HIPAA**: Available with Enterprise Plus plan

---

*For additional enterprise deployment assistance, contact our Professional Services team at enterprise@meeting-processor.com*