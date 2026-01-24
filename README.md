# HC

## About this solution

This is a layered startup solution based on [Domain Driven Design (DDD)](https://abp.io/docs/latest/framework/architecture/domain-driven-design) practises. All the fundamental ABP modules are already installed. Check the [Application Startup Template](https://abp.io/docs/latest/solution-templates/layered-web-application) documentation for more info.

### Pre-requirements

* [.NET10.0+ SDK](https://dotnet.microsoft.com/download/dotnet)
* [Node v18 or 20](https://nodejs.org/en)
* [Redis](https://redis.io/)

### Configurations

The solution comes with a default configuration that works out of the box. However, you may consider to change the following configuration before running your solution:

* Check the `ConnectionStrings` in `appsettings.json` files under the `HC.AuthServer`, `HC.HttpApi.Host` and `HC.DbMigrator` projects and change it if you need.

### Before running the application

* Run `abp install-libs` command on your solution folder to install client-side package dependencies. This step is automatically done when you create a new solution, if you didn't especially disabled it. However, you should run it yourself if you have first cloned this solution from your source control, or added a new client-side package dependency to your solution.
* Run `HC.DbMigrator` to create the initial database. This step is also automatically done when you create a new solution, if you didn't especially disabled it. This should be done in the first run. It is also needed if a new database migration is added to the solution later.

-----
GRANT ALL ON SCHEMA public TO hcs;
GRANT ALL PRIVILEGES ON DATABASE hcs TO hcs;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO hcs;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO hcs;


CREATE SCHEMA IF NOT EXISTS hcs;
ALTER USER hcs SET search_path = hcs, public;



#### Generating a Signing Certificate

In the production environment, you need to use a production signing certificate. ABP Framework sets up signing and encryption certificates in your application and expects an `openiddict.pfx` file in your application.

To generate a signing certificate, you can use the following command:

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p 9fafa8e6-4e2f-41ae-98c4-3dee157c40c6
```

> `9fafa8e6-4e2f-41ae-98c4-3dee157c40c6` is the password of the certificate, you can change it to any password you want.

It is recommended to use **two** RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing.

For more information, please refer to: [OpenIddict Certificate Configuration](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html#registering-a-certificate-recommended-for-production-ready-scenarios)

> Also, see the [Configuring OpenIddict](https://abp.io/docs/latest/Deployment/Configuring-OpenIddict#production-environment) documentation for more information.

### Solution structure

This is a layered monolith application that consists of the following applications:

* `HC.DbMigrator`: A console application which applies the migrations and also seeds the initial data. It is useful on development as well as on production environment.
* `HC.AuthServer`: ASP.NET Core MVC / Razor Pages application that is integrated OAuth 2.0(`OpenIddict`) and account modules. It is used to authenticate users and issue tokens.
* `HC.HttpApi.Host`: ASP.NET Core API application that is used to expose the APIs to the clients.
* `HC.Blazor`: ASP.NET Core Blazor Server application that is the essential web application of the solution.


## Deploying the application

Deploying an ABP application follows the same process as deploying any .NET or ASP.NET Core application. However, there are important considerations to keep in mind. For detailed guidance, refer to ABP's [deployment documentation](https://abp.io/docs/latest/Deployment/Index).

### Additional resources


#### Internal Resources

You can find detailed setup and configuration guide(s) for your solution below:

* [Docker-Compose](./etc/docker-compose/README.md)
* [Docker-Compose for Infrastructure Dependencies](./etc/docker/README.md)
* [Local Kubernetes Guide](./etc/helm/README.md)

#### External Resources
You can see the following resources to learn more about your solution and the ABP Framework:

* [Web Application Development Tutorial](https://abp.io/docs/latest/tutorials/book-store/part-1)
* [Application Startup Template](https://abp.io/docs/latest/startup-templates/application/index)
* [LeptonX Theme Module](https://abp.io/docs/latest/themes/lepton-x/index)
* [LeptonX Blazor UI](https://abp.io/docs/latest/themes/lepton-x/blazor?UI=BlazorServer)
# HCS




docker buildx prune -af
docker system prune -af

docker buildx rm hc-builder
docker buildx create --name hc-builder --use
docker buildx inspect --bootstrap

cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.Blazor && docker build --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-blazor:latest . --push

cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.HttpApi.Host && docker build --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-api:latest . --push

cd /Users/nguyenlong/Documents/Projects/HCS/src/HC.AuthServer && docker build --platform linux/amd64 -f Dockerfile.local -t longnguyen1331/hc-authserver:latest . --push

kill -9 $(lsof -ti :44301)
kill -9 $(lsof -ti :44302)
kill -9 $(lsof -ti :44379)




deploy create cert

cd /www/server/panel/data/compose/hcs/certs

openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
-keyout openiddict.key \
-out openiddict.crt \
-subj "/CN=auth-dev.benhvien199.vn"

openssl pkcs12 -export \
-out openiddict.pfx \
-inkey openiddict.key \
-in openiddict.crt \
-passout pass:StrongPass123
