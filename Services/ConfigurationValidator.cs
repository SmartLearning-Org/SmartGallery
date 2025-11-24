namespace SmartGallery.Services
{
    public class ConfigurationValidator
    {
        public static List<string> ValidateStorageConfiguration(StorageOptions options)
        {
            var errors = new List<string>();

            if (options.UseManagedIdentity)
            {
                if (string.IsNullOrWhiteSpace(options.AccountName))
                {
                    errors.Add("Storage:AccountName er påkrævet når UseManagedIdentity er sat til true");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    errors.Add("Storage:ConnectionString er påkrævet når UseManagedIdentity er sat til false");
                }
                else
                {
                    if (!IsValidConnectionStringFormat(options.ConnectionString))
                    {
                        errors.Add("Storage:ConnectionString har et ugyldigt format. Forventet format: 'name=value;name=value'");
                        errors.Add("Eksempel: 'DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...'");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(options.ContainerName))
            {
                errors.Add("Storage:ContainerName er påkrævet");
            }

            return errors;
        }

        private static bool IsValidConnectionStringFormat(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmedPart))
                    continue;

                if (!trimmedPart.Contains('='))
                    return false;

                var keyValue = trimmedPart.Split('=', 2);
                if (keyValue.Length != 2 || string.IsNullOrWhiteSpace(keyValue[0]))
                    return false;
            }

            return parts.Length > 0;
        }
    }
}
