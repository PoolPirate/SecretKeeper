namespace SecretKeeper.Models;
public enum ConsensusFailureType
{
    SGX_ERROR_BUSY,
    INVALID_APPHASH,
    SOFTWARE_UPGRADE,
    UNKNOWN
}
