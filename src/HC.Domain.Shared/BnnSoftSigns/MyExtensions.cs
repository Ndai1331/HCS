using Bnnsoft.Sdk;
using System;
namespace HC.BnnSoftSigns
{
    public static class MyExtensions
    {
        public static Org.BouncyCastle.X509.X509Certificate[] toX509(this ChainJson chain)
        {

            if (!string.IsNullOrEmpty(chain.sub))
            {
                Org.BouncyCastle.X509.X509Certificate[] rs = new Org.BouncyCastle.X509.X509Certificate[3];
                rs[0] = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(Convert.FromBase64String(chain.end));
                rs[1] = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(Convert.FromBase64String(chain.sub));
                rs[2] = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(Convert.FromBase64String(chain.root));
                return rs;
            }
            else
            {
                Org.BouncyCastle.X509.X509Certificate[] rs = new Org.BouncyCastle.X509.X509Certificate[2];
                rs[0] = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(Convert.FromBase64String(chain.end));
                rs[1] = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(Convert.FromBase64String(chain.root));
                return rs;
            }
        }
    }
}
