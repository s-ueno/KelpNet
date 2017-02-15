﻿using System;
using System.Linq;
using KelpNet.Common;
using KelpNet.Common.Functions;

namespace KelpNet.Functions.Poolings
{
    [Serializable]
    public class MaxPooling : NeedPreviousDataFunction
    {
        private int _kSize;
        private int _stride;
        private int _pad;

        public MaxPooling(int ksize, int stride = 1, int pad = 0, string name = "MaxPooling") : base(name)
        {
            this._kSize = ksize;
            this._stride = stride;
            this._pad = pad;
        }

        protected override BatchArray NeedPreviousForward(BatchArray input)
        {
            int outputSize = (int)Math.Floor((input.Shape[2] - this._kSize + this._pad * 2.0) / this._stride) + 1;
            double[] result = Enumerable.Repeat(double.MinValue, input.Shape[0] * outputSize * outputSize * input.BatchCount).ToArray();

            for (int b = 0; b < input.BatchCount; b++)
            {
                int resultIndex = b * input.Shape[0] * outputSize * outputSize;

                for (int i = 0; i < input.Shape[0]; i++)
                {
                    int inputIndexOffset = i * input.Shape[1] * input.Shape[2];

                    for (int y = 0; y < outputSize; y++)
                    {
                        for (int x = 0; x < outputSize; x++)
                        {
                            for (int dy = 0; dy < this._kSize; dy++)
                            {
                                int inputIndexY = y * this._stride + dy - this._pad;

                                if (inputIndexY >= 0 && inputIndexY < input.Shape[1])
                                {
                                    for (int dx = 0; dx < this._kSize; dx++)
                                    {
                                        int inputIndexX = x * this._stride + dx - this._pad;

                                        if (inputIndexX >= 0 && inputIndexX < input.Shape[2])
                                        {
                                            int inputIndex = inputIndexOffset + inputIndexY * input.Shape[2] + inputIndexX + b * input.Length;
                                            result[resultIndex] = Math.Max(result[resultIndex], input.Data[inputIndex]);
                                        }
                                    }
                                }
                            }

                            resultIndex++;
                        }
                    }
                }
            }

            return BatchArray.Convert(result, new[] { input.Shape[0], outputSize, outputSize }, input.BatchCount);
        }

        protected override BatchArray NeedPreviousBackward(BatchArray gy, BatchArray prevInput, BatchArray prevOutput)
        {
            double[] result = new double[prevInput.Data.Length];

            for (int b = 0; b < gy.BatchCount; b++)
            {
                int index = b * gy.Length;

                for (int i = 0; i < prevInput.Shape[0]; i++)
                {
                    int prevInputIndexOffset = i * prevInput.Shape[1] * prevInput.Shape[2];

                    for (int y = 0; y < prevOutput.Shape[1]; y++)
                    {
                        for (int x = 0; x < prevOutput.Shape[2]; x++)
                        {
                            //前回の入力値と出力値を比較して、同じ値のものを見つける
                            this.SetResult(prevInputIndexOffset, y, x, gy.Data[index], prevInput, prevOutput.Data[index], b, ref result);
                            index++;
                        }
                    }
                }
            }

            return BatchArray.Convert(result, prevInput.Shape, prevInput.BatchCount);
        }

        //同じ値を複数持つ場合、左上優先にして処理を打ち切る
        //他のライブラリの実装では乱数を取って同じ値の中からどれかを選ぶ物が多い
        void SetResult(int prevInputIndexOffset, int y, int x, double data, BatchArray prevInput, double prevOutputData, int b, ref double[] result)
        {
            for (int dy = 0; dy < this._kSize; dy++)
            {
                int outputIndexY = y * this._stride + dy - this._pad;

                if (outputIndexY >= 0 && outputIndexY < prevInput.Shape[1])
                {
                    for (int dx = 0; dx < this._kSize; dx++)
                    {
                        int outputIndexX = x * this._stride + dx - this._pad;

                        if (outputIndexX >= 0 && outputIndexX < prevInput.Shape[2])
                        {
                            int prevInputIndex = prevInputIndexOffset + outputIndexY * prevInput.Shape[2] + outputIndexX + b * prevInput.Length;

                            if (prevInput.Data[prevInputIndex].Equals(prevOutputData))
                            {
                                result[prevInputIndex] = data;
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
