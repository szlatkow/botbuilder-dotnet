﻿// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Bot.Builder.AI.Luis.Testing
{
    /// <summary>
    /// Helper extensions for mocking luis.
    /// </summary>
    public static class MockLuisExtensions
    {
        private static readonly Encoding UTF8 = new UTF8Encoding();

        /// <summary>
        /// Setup configuration to utilize the settings file generated by lubuild.
        /// </summary>
        /// <remarks>
        /// This utilizes the user secrets store to pick up the LUIS endpoint key.  
        /// To store it do: "dotnet user-secrets luis:endpointKey *LUISKEY* --id *UserSecretId*".
        /// The *UserSecretId* should be passed in here.
        /// LUIS responses will be in cachedResponse in <paramref name="directory"/>.
        /// </remarks>
        /// <param name="builder">Configuration builder to modify.</param>
        /// <param name="directory">Directory with the setting file in it.</param>
        /// <param name="userSecretId">The id for finding your user secrets.</param>
        /// <param name="environment">Environment where LUIS models are deployed.</param>
        /// <param name="endpoint">Endpoint to use with a default of westus.</param>
        /// <returns>Modified configuration builder.</returns>
        public static IConfigurationBuilder UseMockLuisSettings(this IConfigurationBuilder builder, string directory, string userSecretId, string environment = null, string endpoint = "https://westus.api.cognitive.microsoft.com")
        {
            if (environment == null)
            {
                environment = Environment.UserName;
            }

            var host = new Uri(endpoint).Host;
            var region = host.Substring(0, host.IndexOf('.'));

            // Add environment luis.settings files to config
            // Either there should be 1 or assume it is cached and any are fine.
            string settings = null;
            var di = new DirectoryInfo(directory);
            foreach (var file in di.GetFiles($"luis.settings.*", SearchOption.AllDirectories))
            {
                settings = file.FullName;
                break;
            }

            return builder
                .SetBasePath(Path.GetFullPath(directory))
                .AddJsonFile(settings, optional: false)
                .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "luis:endpoint", endpoint },
                        { "luis:resources", directory },

                        // Ensure there is a key even if there is no secret
                        { "luis:endpointKey", "00000000-0000-0000-0000-000000000000" }
                    })
                .AddUserSecrets(userSecretId);
        }

        /// <summary>
        /// Creates a hash based on a given string.
        /// </summary>
        /// <param name="source">The string to be hashed.</param>
        /// <returns>Unsigned int hash.</returns>
        public static uint StableHash(this string source)
        {
            uint hash;
            var input = UTF8.GetBytes(source);
            using (var stream = new MemoryStream(input))
            {
                hash = Hash(stream);
            }

            return hash;
        }

        // Implementation of Murmurhash3
        private static uint Hash(Stream stream, uint seed = 0)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            var h1 = seed;
            uint k1;
            var streamLength = 0u;

            using (var reader = new BinaryReader(stream))
            {
                var chunk = reader.ReadBytes(4);
                while (chunk.Length > 0)
                {
                    streamLength += (uint)chunk.Length;
                    switch (chunk.Length)
                    {
                        case 4:
                            k1 = (uint)(chunk[0]
                                       | chunk[1] << 8
                                       | chunk[2] << 16
                                       | chunk[3] << 24);

                            k1 *= c1;
                            k1 = Rotl32(k1, 15);
                            k1 *= c2;

                            h1 ^= k1;
                            h1 = Rotl32(h1, 13);
                            h1 = (h1 * 5) + 0xe6546b64;
                            break;
                        case 3:
                            k1 = (uint)(chunk[0]
                                      | chunk[1] << 8
                                      | chunk[2] << 16);
                            k1 *= c1;
                            k1 = Rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                        case 2:
                            k1 = (uint)(chunk[0]
                                      | chunk[1] << 8);
                            k1 *= c1;
                            k1 = Rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                        case 1:
                            k1 = (uint)chunk[0];
                            k1 *= c1;
                            k1 = Rotl32(k1, 15);
                            k1 *= c2;
                            h1 ^= k1;
                            break;
                    }

                    chunk = reader.ReadBytes(4);
                }
            }

            h1 ^= streamLength;
            h1 = FMix(h1);

            return h1;
        }

        private static uint Rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private static uint FMix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }
}