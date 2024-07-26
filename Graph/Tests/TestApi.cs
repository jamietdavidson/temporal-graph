using Graph.Api;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Snapshooter.NUnit;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Graph.Tests;

public class TestApi
{
    private string _nodeFragment = @"
        fragment NodeFragment on Node {
            id
            tag
            timestamp
            fields {
                key
                value
            }
            edges {
                id
                key
                tag
            }
            edgeCollections {
                ids
                key
                tag
                count
                pageInfo {
                    hasNextPage
                    hasPreviousPage
                    firstId
                    lastId
                }
            }
        }
    ";

    private string _nodeFragmentWithoutId = @"
        fragment NodeFragment on Node {
            tag
            fields {
                key
                value
            }
            edges {
                key
                tag
            }
            edgeCollections {
                key
                tag
                count
                pageInfo {
                    hasNextPage
                    hasPreviousPage
                    firstId
                    lastId
                }
            }
        }
    ";

    [Test]
    public async Task TestSchemalessCreate()
    {
        var executor = await new ServiceCollection()
            .AddGraphQL()
            .AddQueryType<QueryType>()
            .AddMutationType<MutationType>()
            .BuildRequestExecutorAsync();

        var result = await executor.ExecuteAsync(@"mutation {
            updateGraph (
                operations: {
                    creations: [
                        {
                            tag: ""Bicycle"",
                            fields: [
                                {
                                    key: ""Name"",
                                    value: ""Speedster""
                                },
                                {
                                    key: ""Color"",
                                    value: ""Red""
                                },
                                {
                                    key: ""Price"",
                                    value: 999.99
                                }
                            ]
                        },
                        { tag: ""Saddle"", fields: [] },
                        { tag: ""Wheel"", fields: [] },
                        { tag: ""Wheel"", fields: [] },
                        { tag: ""Spoke"", fields: [] },
                        { tag: ""Spoke"", fields: [] }
                    ],
                    edgesToSet: [
                        {
                            fromCreatedNodeIndex: 0,
                            toCreatedNodeIndex: 1,
                            key: ""Seat""
                        }
                    ]
                    edgeCollectionAdditions: [
                        {
                            key: ""Wheels""
                            fromCreatedNodeIndex: 0
                            toCreatedNodeIndices: [2, 3]
                        },
                        {
                            key: ""Spokes""
                            fromCreatedNodeIndex: 2
                            toCreatedNodeIndices: [4]
                        },
                        {
                            key: ""Spokes""
                            fromCreatedNodeIndex: 2
                            toCreatedNodeIndices: [5]
                        }
                    ]
                }
            ) {
                state
                nodes {
                    ...NodeFragment
                }
                additionalContext {
                    createdNodes {
                        ...NodeFragment
                    }
                    deletedNodeIds
                }
            }
        } " + _nodeFragmentWithoutId);

        result.MatchSnapshot();
    }

    [Test]
    public void TestGenerateAndValidateToken()
    {
        // Test token generation
        var userId = 12345;
        var permissions = new[] { "read", "party", "sleep" };
        var token = Utils.GenerateToken(userId, permissions);
        var tokenRaw = Utils.GenerateTokenRaw(userId, permissions);

        Assert.That(tokenRaw, Is.EqualTo(token));

        // Test token validation
        var isTokenValid = Utils.ValidateToken(token);
        var isTokenRawValid = Utils.ValidateToken(tokenRaw);

        // Test invalid token validation fails
        var splitToken = token.Split('.');
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(Base64UrlEncoder.Decode(splitToken[1]));
        if (payload != null && payload.ContainsKey("permissions") && payload["permissions"] != null)
        {
            payload["permissions"] = payload["permissions"] + ",write";
        }
        else
        {
            Assert.Fail();
        }
        splitToken[1] = Base64UrlEncoder.Encode(JsonSerializer.Serialize(payload));
        var additionalPermissionsToken = splitToken[0] + "." + splitToken[1] + "." + splitToken[2];
        var isAdditionalTokenValid = Utils.ValidateToken(additionalPermissionsToken);

        Assert.IsTrue(isTokenValid);
        Assert.IsTrue(isTokenRawValid);
        Assert.IsFalse(isAdditionalTokenValid);

        // Test claims
        var userIdTokenClaim = Utils.GetClaim(token, "userId");
        var permissionsTokenClaim = Utils.GetClaim(token, "permissions");
        var userIdTokenRawClaim = Utils.GetClaim(tokenRaw, "userId");
        var permissionsTokenRawClaim = Utils.GetClaim(tokenRaw, "permissions");

        Assert.That(userId.ToString(), Is.EqualTo(userIdTokenClaim));
        Assert.That(string.Join(",", permissions), Is.EqualTo(permissionsTokenClaim));
        Assert.That(userId.ToString(), Is.EqualTo(userIdTokenRawClaim));
        Assert.That(string.Join(",", permissions), Is.EqualTo(permissionsTokenRawClaim));

        // Test invalid token claims
        try
        {
            var userIdAdditionalTokenClaim = Utils.GetClaim(additionalPermissionsToken, "userId");
            Assert.Fail();
        }
        catch (Exception e)
        {
            Assert.That(e.Message, Is.EqualTo("Invalid token."));
        }
    }
}