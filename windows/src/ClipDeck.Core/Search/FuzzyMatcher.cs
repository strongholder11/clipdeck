namespace ClipDeck.Core.Search;

public readonly record struct FuzzyMatch(double Score, IReadOnlyList<int> MatchedIndices);

/// <summary>
/// Casamento fuzzy por subsequência com pontuação.
///
/// Digitar "flwp" acha "Follow-up pós-reunião". A programação dinâmica existe
/// porque o laço guloso óbvio casa a primeira ocorrência de cada letra e erra o
/// ranking; a DP encontra o melhor alinhamento, então início de palavra e letras
/// coladas ganham como deveriam.
/// </summary>
public static class FuzzyMatcher
{
    private const double MatchScore = 1.0;
    private const double WordStartBonus = 1.5;
    private const double ConsecutiveBonus = 1.2;
    private const double GapPenalty = -0.15;
    private const double FirstCharBonus = 0.8;

    /// <summary>
    /// Limite de texto analisado. O corpo de um template pode ter milhares de
    /// caracteres e a busca roda a cada tecla digitada.
    /// </summary>
    public const int MaxCandidateLength = 2_000;

    /// <summary>
    /// Retorna null se nem todos os caracteres da consulta aparecem em ordem.
    /// <paramref name="trackIndices"/> custa memória; só vale para o campo que
    /// será destacado na tela (o título).
    /// </summary>
    public static FuzzyMatch? Match(
        string query,
        string candidate,
        HashSet<int> wordStarts,
        bool trackIndices = false)
    {
        if (string.IsNullOrEmpty(query)) return new FuzzyMatch(0, Array.Empty<int>());

        if (candidate.Length > MaxCandidateLength)
        {
            candidate = candidate[..MaxCandidateLength];
        }

        var n = candidate.Length;
        var m = query.Length;
        if (m > n) return null;

        const double negativeInfinity = double.NegativeInfinity;

        var previousRow = new double[n];
        var currentRow = new double[n];
        Array.Fill(previousRow, negativeInfinity);
        Array.Fill(currentRow, negativeInfinity);

        var parents = trackIndices ? new int[m][] : null;

        for (var i = 0; i < m; i++)
        {
            if (trackIndices)
            {
                parents![i] = new int[n];
                Array.Fill(parents[i], -1);
            }

            // Melhor valor da linha anterior até j-2, permitindo um salto com
            // penalidade. Mantido incrementalmente para não virar O(n²).
            var bestBefore = negativeInfinity;
            var bestBeforeIndex = -1;

            for (var j = 0; j < n; j++)
            {
                if (candidate[j] == query[i])
                {
                    var bonus = MatchScore;
                    if (wordStarts.Contains(j)) bonus += WordStartBonus;
                    if (j == 0) bonus += FirstCharBonus;

                    if (i == 0)
                    {
                        // Casar mais perto do início do texto é ligeiramente melhor.
                        currentRow[j] = bonus - j * 0.01;
                    }
                    else
                    {
                        // j == 0 não tem caractere anterior, então não existe
                        // casamento consecutivo possível.
                        var consecutive = j >= 1 && !double.IsNegativeInfinity(previousRow[j - 1])
                            ? previousRow[j - 1] + bonus + ConsecutiveBonus
                            : negativeInfinity;

                        var jumped = !double.IsNegativeInfinity(bestBefore)
                            ? bestBefore + bonus + GapPenalty
                            : negativeInfinity;

                        if (consecutive >= jumped && !double.IsNegativeInfinity(consecutive))
                        {
                            currentRow[j] = consecutive;
                            if (trackIndices) parents![i][j] = j - 1;
                        }
                        else if (!double.IsNegativeInfinity(jumped))
                        {
                            currentRow[j] = jumped;
                            if (trackIndices) parents![i][j] = bestBeforeIndex;
                        }
                        else
                        {
                            currentRow[j] = negativeInfinity;
                        }
                    }
                }
                else
                {
                    currentRow[j] = negativeInfinity;
                }

                // Só entra no acumulado depois de usado, garantindo que o salto
                // sempre venha de uma posição estritamente anterior.
                if (i > 0 && j >= 1 && previousRow[j - 1] > bestBefore)
                {
                    bestBefore = previousRow[j - 1];
                    bestBeforeIndex = j - 1;
                }
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        var bestEnd = -1;
        var bestScore = negativeInfinity;
        for (var j = 0; j < n; j++)
        {
            if (previousRow[j] > bestScore)
            {
                bestScore = previousRow[j];
                bestEnd = j;
            }
        }

        if (bestEnd < 0 || double.IsNegativeInfinity(bestScore)) return null;

        // Normaliza pelo tamanho da consulta, para consultas de tamanhos
        // diferentes serem comparáveis, e favorece candidatos curtos.
        var normalized = bestScore / m * (1.0 + 12.0 / (12.0 + n));

        var indices = Array.Empty<int>();
        if (trackIndices)
        {
            var collected = new List<int>(m);
            var j = bestEnd;
            for (var i = m - 1; i >= 0 && j >= 0; i--)
            {
                collected.Add(j);
                j = parents![i][j];
            }
            collected.Reverse();
            indices = collected.ToArray();
        }

        return new FuzzyMatch(normalized, indices);
    }
}
