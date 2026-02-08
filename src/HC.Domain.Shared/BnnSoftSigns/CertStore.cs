using Bnnsoft.Sdk;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace HC.BnnSoftSigns
{
    public class CertStore
    {
        static CertStore? _inst;
        public static CertStore Instance
        {
            get
            {
                if (_inst == null)
                {
                    _inst = new CertStore();
                }
                return _inst;
            }
        }
        readonly MemoryCache cache;
        public CertStore()
        {
            cache = new MemoryCache(new MemoryCacheOptions()
            {

            });

        }

        public static Org.BouncyCastle.X509.X509Certificate getX509(VinHsmServiceClient signClient)
        {
            var certbyte = signClient.GetEndCertfiticate();
            return new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(certbyte);
        }

        public Org.BouncyCastle.X509.X509Certificate[] getX509Chain(VinHsmServiceClient signClient)
        {
            var item = cache.Get<ChainJson>(signClient.Apikey);
            if (item == null)
            {
                item = signClient.GetCertfiticateChain();
                cache.Set(signClient.Apikey, item, TimeSpan.FromMinutes(30));
            }
            return item.toX509();
        }
    }
}
