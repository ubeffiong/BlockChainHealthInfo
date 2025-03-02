**Healthcare Blockchain Audit & Digital Signature System**

Overview
A secure healthcare records system combining blockchain technology with advanced digital signatures for tamper-proof auditing and data integrity verification.

Key Features
🔒 Blockchain-based audit trail with cryptographic chaining

📝 ECDSA + HMAC dual-signature mechanism

⏳ Time-bound cryptographic keys with automatic rotation

🔄 Immutable version history for all medical records

🔍 Configurable audit comparison engine

Digital Signature Flow
Key Components
Component	                  | Purpose	                     | Technology
KeyGenerator	                Cryptographic key management	 ECDSA (P-521), HKDF
DigitalSignatureService	      Data signing/verification	     HMAC-SHA256, ECDSA
BlockchainService	            Audit trail management	       HMAC-SHA256 chaining
AuditTrailService	            Change tracking & comparison	 JSON Diff, GZip
KeyRotationService	          Key lifecycle management	     Argon2, ChaCha20

**Key Management**
Lifecycle Workflow
1. Key Rotation (KeyRotationService.cs)
   20% key shift every 2 hours
   Full regeneration every 24 hours
   Argon2 memory-hard derivation

2. Storage Security
   Environment variable secrets (smart-contract)
   Encrypted ZIP archives
   Memory-protected keys

Blockchain Audit Flow
Audit Process
-  Change Detection (AuditTrailService.LogChanges())
-  Deep object comparison
-  Property-level change tracking
-  Related data inclusion
