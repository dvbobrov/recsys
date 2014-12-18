using System;
using System.Collections.Generic;
using System.IO;

namespace svd
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("too few arguments");
                return;
            }

            string testFile = args[0];
            string resFile = args[1];
            IdMap userIds = new IdMap(3500);
            IdMap itemIds = new IdMap(5000);

            List<UserRatingModel> models = new List<UserRatingModel>(1000000);
            for (int i = 2; i < args.Length; i++)
            {
                ReadTrainingFile(args[i], models, userIds, itemIds);
            }
            byte[,] ratings = new byte[userIds.Count, itemIds.Count];
            foreach (var model in models)
            {
                ratings[model.UserId, model.ItemId] = model.Rating;
            }

            SvdImpl svd = new SvdImpl(ratings, userIds, itemIds);
            svd.Learn();

            using (var reader = new StreamReader(File.OpenRead(testFile)))
            using (var writer = new StreamWriter(File.OpenWrite(resFile)))
            {
                reader.ReadLine();
                writer.WriteLine("id,rating");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split(',');
                    int rating = svd.PredictRating(long.Parse(split[1]), long.Parse(split[2]));
                    writer.WriteLine("{0},{1}", split[0], rating);
                }
            }
        }

        private static void ReadTrainingFile(string trainFile, List<UserRatingModel> models, IdMap userIds, IdMap itemIds)
        {
            using (var reader = new StreamReader(File.OpenRead(trainFile)))
            {
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] split = line.Split(',');
                    models.Add(new UserRatingModel
                    {
                        UserId = userIds.GetOrInsert(long.Parse(split[0])),
                        ItemId = itemIds.GetOrInsert(long.Parse(split[1])),
                        Rating = byte.Parse(split[2])
                    });
                }
            }
        }
    }
}