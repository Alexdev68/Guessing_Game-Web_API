using GuessingGame.API.Models;
using System;
using System.Collections.Generic;

namespace GuessingGame.API.Services
{
    internal class RandomGenerator
    {
        public static List<string> Generate(GameConfig config)
        {
            List<string> values = new();
            Random rand = new Random();

            while (values.Count < config.GuessLength)
            {
                string value;

                if (config.AllowAlphanumeric && rand.Next(2) == 0)
                {
                    value = ((char)rand.Next('A', 'Z' + 1)).ToString();
                }
                else
                {
                    value = rand.Next(config.MinValue, config.MaxValue + 1).ToString();
                }

                if (!config.AllowDuplicates && values.Contains(value))
                {
                    continue;
                }

                values.Add(value);
            }

            return values;
        }
    }
}