using System;
using System.Linq;
using System.Threading;

namespace svd
{
    internal class SvdImpl
    {
        private const double Gamma = 0.005;
        private const double Lambda = 0.02;
        private const int Size = 10;
        private const double Threshold = 50;

        private readonly byte[,] _ratings;

        private readonly int _uCount;
        private readonly int _iCount;

        private readonly double[] _baseUser;
        private readonly double[] _baseItem;
        private readonly double[,] _p;
        private readonly double[,] _q;

        private readonly IdMap _userIds;
        private readonly IdMap _itemIds;

        private double _avg = 0.0;

        public SvdImpl(byte[,] ratings, IdMap userIds, IdMap itemIds)
        {
            _ratings = ratings;
            _userIds = userIds;
            _itemIds = itemIds;

            _uCount = ratings.GetLength(0);
            _iCount = ratings.GetLength(1);

            _baseUser = new double[_uCount];
            _baseItem = new double[_iCount];

            _p = new double[_uCount, Size];
            _q = new double[_iCount, Size];

            Random prng = new Random();
            for (int i = 0; i < _uCount; i++)
            {
                for (int j = 0; j < Size; j++)
                {
                    _p[i, j] = prng.NextDouble();
                }
            }
            for (int i = 0; i < _iCount; i++)
            {
                for (int j = 0; j < Size; j++)
                {
                    _q[i, j] = prng.NextDouble();
                }
            }
        }

        public void Learn()
        {
            for (int j = 0; j < _iCount; j++)
            {
                double itemRating = 0;
                double count = 0;
                for (int i = 0; i < _uCount; i++)
                {
                    if (_ratings[i, j] != 0)
                    {
                        itemRating += _ratings[i, j];
                        count++;
                    }
                }
                _avg += (itemRating/count);
            }
            _avg /= _iCount;

            double rmse = 0;
            while (true)
            {
                for (int userId = 0; userId < _uCount; userId++)
                {
                    for (int itemId = 0; itemId < _iCount; itemId++)
                    {
                        if (_ratings[userId, itemId] != 0)
                        {
                            double predictionError = CalculateSinglePredictionError(userId, itemId);
                            _baseUser[userId] = _baseUser[userId] + Gamma*(predictionError - Lambda*_baseUser[userId]);
                            _baseItem[itemId] = _baseItem[itemId] + Gamma*(predictionError - Lambda*_baseItem[itemId]);
                            for (int i = 0; i < Size; i++)
                            {
                                double p = _p[userId, i];
                                double q = _q[itemId, i];
                                _p[userId, i] = p + Gamma*(predictionError*q - Lambda*p);
                                _q[itemId, i] = q + Gamma*(predictionError*p - Lambda*q);
                            }
                        }
                    }
                }
                double newRmse = CalculateSquaredError();
                if (Math.Abs(newRmse - rmse) < Threshold)
                {
                    return;
                }
                rmse = newRmse;
            }
        }

        private double CalculateSquaredError()
        {
            CountdownEvent countdown = new CountdownEvent(1);

            double[] errMain = new double[2];

            ThreadPool.QueueUserWorkItem(state =>
            {
                ((double[]) state)[0] = CalculateErrorPartial(0, _uCount / 2);
                countdown.Signal();
            }, errMain);

            errMain[1] = CalculateErrorPartial(_uCount/2, _uCount);

            double errPQ = 0.0;
            for (int i = 0; i < _uCount; i++)
            {
                double errDelta = 0;
                for (int j = 0; j < Size; j++)
                {
                    errDelta += Math.Pow(_p[i, j], 2);
                }
                errPQ += Lambda*errDelta;
            }

            for (int i = 0; i < _iCount; i++)
            {
                double errDelta = 0;
                for (int j = 0; j < Size; j++)
                {
                    errDelta += Math.Pow(_q[i, j], 2);
                }

                errPQ += Lambda*errDelta;
            }

            var error = Lambda*(_baseItem.Sum(x => x*x) + _baseUser.Sum(x => x*x));
            countdown.Wait();
            error = error + errMain[0] + errMain[1] + errPQ;
            return error;
        }

        private double CalculateErrorPartial(int start, int end)
        {
            double res = 0.0;
            for (int i = start; i < end; i++)
            {
                for (int j = 0; j < _iCount; j++)
                {
                    if (_ratings[i, j] != 0)
                    {
                        res += Math.Pow(CalculateSinglePredictionError(i, j), 2);
                    }
                }
            }

            return res;
        }

        public int PredictRating(long userId, long itemId)
        {
            int internalUid = _userIds[userId];
            int internalIid = _itemIds[itemId];

            double itemBaseline = internalUid < 0 ? 0 : _baseUser[internalUid];
            double userBaseline = internalIid < 0 ? 0 : _baseItem[internalIid];
            double pq = internalIid < 0 || internalUid < 0 ? 0 : CalculateQP(internalUid, internalIid);
            var predictedRating = (int)Math.Round(_avg + itemBaseline + userBaseline + pq);
            if (predictedRating == 0)
            {
                predictedRating++;
            }

            return predictedRating;
        }

        private double CalculateSinglePredictionError(int userId, int itemId)
        {
            return _ratings[userId, itemId] - _avg - _baseUser[userId] - _baseItem[itemId] - CalculateQP(userId, itemId);
        }

        // ReSharper disable once InconsistentNaming
        private double CalculateQP(int userId, int itemId)
        {
            double res = 0;
            for (int i = 0; i < Size; i++)
            {
                res += _p[userId, i]*_q[itemId, i];
            }

            return res;
        }
    }
}