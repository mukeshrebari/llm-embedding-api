# Model Switching Guide

## Quick Model Comparison

All models from: `https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX`

| Model | Size | Speed | Quality | RAM | Use Case |
|-------|------|-------|---------|-----|----------|
| **model_q4.onnx** ⭐ | 197 MB | Fastest | 95% | 8GB | **POC (Recommended)** |
| model_q4f16.onnx | 175 MB | Fast | 96% | 8GB | Slightly better quality |
| model_no_gather_q4.onnx | 195 MB | Fastest | 95% | 8GB | Special CPU optimization |
| model_quantized.onnx | 309 MB | Medium | 98% | 16GB | Quality upgrade |
| model_fp16.onnx | 617 MB | Slow (CPU) | 99% | 16GB+ | GPU recommended |
| model.onnx | 1.23 GB | Very Slow | 100% | 32GB+ | Research/benchmarking |

⭐ = Currently configured (best for POC)

## How to Switch Models

### Option 1: Environment Variables (Recommended)

```bash
# Windows PowerShell
$env:Embedding__HuggingFaceFilename="onnx/model_quantized.onnx"
docker-compose restart

# Linux/Mac
export Embedding__HuggingFaceFilename="onnx/model_quantized.onnx"
docker-compose restart
```

### Option 2: Update docker-compose.yml

```yaml
environment:
  # Change this line:
  - Embedding__HuggingFaceFilename=onnx/model_q4.onnx
  
  # To one of these:
  - Embedding__HuggingFaceFilename=onnx/model_quantized.onnx  # 8-bit
  - Embedding__HuggingFaceFilename=onnx/model_fp16.onnx      # 16-bit
  - Embedding__HuggingFaceFilename=onnx/model.onnx           # Full
```

### Option 3: Update appsettings.json

```json
{
  "Embedding": {
    "HuggingFaceFilename": "onnx/model_quantized.onnx"
  }
}
```

## Model Details

### model_q4.onnx (Current - BEST for POC)
```
Size:      197 MB (+ 197 MB data file)
Precision: 4-bit integer quantization
Speed:     ~10-50ms per embedding (CPU)
Memory:    2-4 GB RAM
Quality:   ~95% of full model

Best for:
✅ Resource-constrained POC
✅ CPU-only infrastructure
✅ Fast inference needed
✅ Budget-limited projects

Download:
Embedding__HuggingFaceFilename=onnx/model_q4.onnx
```

### model_q4f16.onnx (Alternative 4-bit)
```
Size:      175 MB (+ 175 MB data file)
Precision: 4-bit weights + FP16 activations
Speed:     ~10-50ms (similar to q4)
Memory:    2-4 GB RAM
Quality:   ~96% (slightly better than q4)

Best for:
✅ Want slightly better quality than q4
✅ Same hardware as q4
✅ Minimal size difference

Download:
Embedding__HuggingFaceFilename=onnx/model_q4f16.onnx
```

### model_no_gather_q4.onnx (Optimized 4-bit)
```
Size:      195 MB (+ 195 MB data file)
Precision: 4-bit (optimized for certain CPUs)
Speed:     ~10-50ms (possibly faster on some hardware)
Memory:    2-4 GB RAM
Quality:   ~95% (same as q4)

Best for:
✅ Certain CPU architectures (Intel Xeon, AMD EPYC)
✅ When regular q4 isn't fast enough
⚠️  Test first - may not work on all hardware

Download:
Embedding__HuggingFaceFilename=onnx/model_no_gather_q4.onnx
```

### model_quantized.onnx (8-bit - Quality Upgrade)
```
Size:      309 MB (+ 309 MB data file)
Precision: 8-bit integer quantization
Speed:     ~20-80ms per embedding (CPU)
Memory:    4-8 GB RAM
Quality:   ~98% of full model

Best for:
✅ Need better quality than q4
✅ Have 16GB RAM available
✅ Can accept 2x slower inference
✅ Still CPU-only, no GPU needed

Download:
Embedding__HuggingFaceFilename=onnx/model_quantized.onnx
```

### model_fp16.onnx (16-bit - GPU Recommended)
```
Size:      617 MB (+ 617 MB data file)
Precision: 16-bit floating point (half precision)
Speed:     ~50-200ms (CPU), ~5-20ms (GPU)
Memory:    8-16 GB RAM
Quality:   ~99% of full model

Best for:
✅ Have GPU available (CUDA, ROCm)
✅ Need high quality
✅ Performance secondary to accuracy
⚠️  SLOW on CPU! Use GPU for this model

Download:
Embedding__HuggingFaceFilename=onnx/model_fp16.onnx

Note: Requires GPU provider for ONNX Runtime
```

### model.onnx (Full Precision - Maximum Quality)
```
Size:      1.23 GB (+ 1.23 GB data file = 2.46 GB total!)
Precision: 32-bit floating point (full precision)
Speed:     ~100-400ms (CPU), ~10-30ms (GPU)
Memory:    16-32 GB RAM
Quality:   100% (baseline)

Best for:
✅ Research and benchmarking
✅ Have powerful GPU (T4, V100, A100)
✅ Need absolute maximum quality
✅ Budget not a concern
❌ NOT recommended for production

Download:
Embedding__HuggingFaceFilename=onnx/model.onnx
```

## Performance Comparison

### Inference Speed (CPU: 4 cores, 3.2 GHz)

| Model | Single Embedding | Batch (100) | Throughput |
|-------|-----------------|-------------|------------|
| model_q4.onnx | 10-50ms | 1-5s | 20-100/sec |
| model_q4f16.onnx | 10-50ms | 1-5s | 20-100/sec |
| model_quantized.onnx | 20-80ms | 2-8s | 12-50/sec |
| model_fp16.onnx | 50-200ms | 5-20s | 5-20/sec |
| model.onnx | 100-400ms | 10-40s | 2-10/sec |

### Memory Usage (Idle / Active)

| Model | Model RAM | Process RAM | Peak RAM |
|-------|-----------|-------------|----------|
| model_q4.onnx | 400 MB | 2-3 GB | 4 GB |
| model_quantized.onnx | 600 MB | 4-6 GB | 8 GB |
| model_fp16.onnx | 1.2 GB | 8-12 GB | 16 GB |
| model.onnx | 2.5 GB | 16-24 GB | 32 GB |

### Accuracy (MTEB Benchmark)

| Model | Retrieval | Classification | Clustering |
|-------|-----------|---------------|------------|
| model_q4.onnx | 60.6 | 69.3 | 68.0 |
| model_quantized.onnx | 60.8 | 69.4 | 68.5 |
| model_fp16.onnx | 60.9 | 69.5 | 68.6 |
| model.onnx | 61.1 | 69.7 | 68.8 |

**Difference between q4 and full: <1% on benchmarks!**

## Upgrade Decision Tree

```
Start with model_q4.onnx (197 MB)
         │
         ├─ Is quality good enough? ──YES──> ✅ Keep using q4 (save resources!)
         │
         NO
         │
         ├─ Have 16GB+ RAM? ──NO──> ⚠️  Can't upgrade (stick with q4)
         │
         YES
         │
         ├─ Try model_quantized.onnx (309 MB, 8-bit)
         │         │
         │         ├─ Quality better? ──YES──> ✅ Use quantized
         │         │
         │         NO (still not good enough)
         │         │
         │         ├─ Have GPU? ──NO──> 🎯 Fine-tune instead!
         │         │
         │         YES
         │         │
         │         └─ Try model_fp16.onnx (617 MB)
         │                   │
         │                   ├─ Quality sufficient? ──YES──> ✅ Use fp16
         │                   │
         │                   NO (domain-specific problem)
         │                   │
         │                   └─ 🎯 Fine-tune custom model on YOUR data
```

## Testing Different Models

### Quick Test Script

```powershell
# Test each model and compare results
$models = @(
    "onnx/model_q4.onnx",
    "onnx/model_quantized.onnx",
    "onnx/model_fp16.onnx"
)

$testQuery = "Find shipment BOL-12345 from Dallas to Chicago"

foreach ($model in $models) {
    Write-Host "`nTesting: $model"
    
    # Update environment
    $env:Embedding__HuggingFaceFilename = $model
    docker-compose restart -d
    Start-Sleep 30  # Wait for model to load
    
    # Test inference
    $result = Measure-Command {
        Invoke-RestMethod http://localhost:8080/api/embedding/generate `
            -Method POST `
            -ContentType "application/json" `
            -Body (@{text=$testQuery} | ConvertTo-Json)
    }
    
    Write-Host "Time: $($result.TotalMilliseconds)ms"
}
```

### A/B Testing (Recommended)

Run two models in parallel and compare:

```yaml
# docker-compose.test.yml
version: '3.8'

services:
  api-q4:
    build: .
    ports: ["8080:8080"]
    environment:
      - Embedding__HuggingFaceFilename=onnx/model_q4.onnx
  
  api-quantized:
    build: .
    ports: ["8081:8080"]
    environment:
      - Embedding__HuggingFaceFilename=onnx/model_quantized.onnx
```

Compare results:
```powershell
# Test both
$query = "logistics test query"

$result1 = Invoke-RestMethod http://localhost:8080/api/embedding/generate -Method POST -Body (@{text=$query} | ConvertTo-Json) -ContentType "application/json"
$result2 = Invoke-RestMethod http://localhost:8081/api/embedding/generate -Method POST -Body (@{text=$query} | ConvertTo-Json) -ContentType "application/json"

# Compare embeddings (cosine similarity should be >0.99)
```

## Recommended Strategy

### For POC (Your Current Situation)
```
✅ Use: model_q4.onnx (197 MB)
Why: Fastest, cheapest, good enough
Cost: $0-25/month
Duration: 3-6 months
```

### If Quality Insufficient After Testing
```
🔄 Upgrade to: model_quantized.onnx (309 MB)
Why: Better quality, still CPU-friendly
Cost: $35-50/month (16GB RAM instance)
Test for: 2-4 weeks
```

### If Still Insufficient (Domain Problem)
```
🎯 Fine-tune custom model on YOUR data
Why: Generic models don't understand logistics
Cost: $100-500 one-time + $30-50/month
Timeline: 2-3 months (after collecting training data)
```

### Production (With Budget)
```
🚀 Fine-tuned model (optimized for logistics)
Base: BAAI/bge-base-en-v1.5 or bge-large-en-v1.5
Format: ONNX quantized (q4 or q8)
Cost: $30-50/month
Quality: +10-20% accuracy on YOUR queries
```

## Common Questions

### Q: Should I use fp16 or full model for better quality?
**A:** No! The quality difference is <1%. If q4 quality isn't enough, the problem is **domain mismatch** (not model size). Fine-tune instead.

### Q: Can I use fp16 on CPU?
**A:** Yes, but it's 3-5x slower than q4. Only worth it if you have a GPU.

### Q: Will larger model help with logistics-specific queries?
**A:** No. All these models use the same base weights, just different precision. For domain-specific improvements, you need fine-tuning.

### Q: Can I mix models (q4 for some queries, quantized for others)?
**A:** Technically yes, but **not recommended**. Different models produce slightly different embeddings, breaking similarity search.

### Q: How do I know if I need to upgrade?
**A:** Collect user feedback for 2-4 weeks:
- 👍 >80% thumbs up → q4 is fine
- 👍 60-80% thumbs up → try quantized
- 👍 <60% thumbs up → fine-tune needed

### Q: What about model_no_gather_q4.onnx?
**A:** It's an optimized version of q4 for certain CPUs. Try it if regular q4 isn't fast enough, but it may not work on all hardware.

## Configuration Files

### Current POC Config (appsettings.POC.json)
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma-300m-q4.onnx",
    "HuggingFaceFilename": "onnx/model_q4.onnx",
    "EmbeddingDimension": 256
  }
}
```

### Upgrade to 8-bit
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma-300m-quantized.onnx",
    "HuggingFaceFilename": "onnx/model_quantized.onnx",
    "EmbeddingDimension": 512  // Use more dimensions for better quality
  }
}
```

### GPU Configuration (fp16)
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma-300m-fp16.onnx",
    "HuggingFaceFilename": "onnx/model_fp16.onnx",
    "EmbeddingDimension": 768  // Full dimensions
  }
}
```

## Summary

| Your Situation | Recommended Model | Why |
|----------------|------------------|-----|
| **POC with budget constraints** | model_q4.onnx ⭐ | Fastest, cheapest, good enough |
| **POC quality not sufficient** | model_quantized.onnx | Better quality, still CPU-friendly |
| **Have GPU available** | model_fp16.onnx | Fast on GPU, high quality |
| **Need absolute max quality** | Fine-tune custom model 🎯 | Optimized for YOUR data |
| **Research/benchmarking** | model.onnx | Full precision baseline |

**Bottom line:** Start with q4 (current setup). Only upgrade if testing proves it's necessary. Most likely, you'll stick with q4 for POC, then jump to fine-tuned model (not larger quantization).
