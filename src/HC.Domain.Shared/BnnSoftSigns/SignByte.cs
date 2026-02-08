using Bnnsoft.Sdk;

namespace HC.BnnSoftSigns
{
    public class SignByte : Bnn.SignLib.IByteSigner
    {
        private readonly VinHsmServiceClient signClient;

        public SignByte(VinHsmServiceClient signClient)
        {
            this.signClient = signClient;
        }

        public byte[] Sign(byte[] digestInfo)
        {
            return signClient.Sign(digestInfo);
        }
    }

}

