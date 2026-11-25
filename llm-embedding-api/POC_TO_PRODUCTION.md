# POC to Production Migration Guide

## Current POC Setup (Cost-Effective)

**Model:** `onnx-community/embeddinggemma-300m-ONNX` (q4 quantized)
- **Cost:** $0/month (self-hosted)
- **Size:** 197 MB
- **Speed:** ~10-50ms per embedding
- **Dimensions:** 256d (or 128d for faster POC)
- **MTEB Score:** ~60 (good enough for POC)

## Why This Works for POC

✅ **Zero Cost** - No API fees, runs on modest hardware  
✅ **Fast Setup** - Already configured, just `docker-compose up`  
✅ **Validates Concept** - Tests retrieval/search workflows  
✅ **Collects Metrics** - Measures actual usage patterns  
✅ **Easy Migration** - Can swap models later without code changes

## Future Production Options

### Option 1: Fine-tuned Model (Recommended)

**Timeline:** When you have budget (~3-6 months)  
**Cost:** $100-500 one-time + hosting  
**Performance Gain:** +10-20% accuracy on logistics queries

**Model Candidates:**
| Model | Size | MTEB | Why | Fine-tune Cost |
|-------|------|------|-----|----------------|
| `BAAI/bge-base-en-v1.5` | 110M | 63.55 | Best for fine-tuning | $50-100 |
| `BAAI/bge-large-en-v1.5` | 335M | 63.98 | Higher accuracy | $100-200 |
| `intfloat/e5-base-v2` | 110M | 62.25 | Good instruction following | $50-100 |

**Fine-tuning Dataset (collect during POC):**
```
Your actual logistics queries:
- "find shipment BOL-12345"
- "track container TCNU1234567"
- "FTL quote from Dallas to Chicago"
- "customs clearance documents for HS code 8471.30"
```

**Steps:**
1. During POC, log all search queries and their results
2. Have users rate relevance (thumbs up/down)
3. Create training dataset: (query, positive_doc, negative_doc)
4. Fine-tune using Sentence Transformers
5. Convert to ONNX (same pipeline as current)
6. Re-embed all documents (~1-2 hours for 100k docs)

### Option 2: Larger Pre-trained Model

**Timeline:** Immediate (if you get better hardware)  
**Cost:** $0-50/month (GPU hosting)

**Model Upgrade Path:**
```
Current:  embeddinggemma-300m-q4  (197 MB, CPU)
    ↓
Upgrade:  bge-large-en-v1.5       (1.3 GB, CPU/GPU)
          +3 MTEB points, no fine-tuning needed
    ↓
Best:     Fine-tuned bge-large    (1.3 GB, GPU)
          +10-15 MTEB points on YOUR data
```

### Option 3: Hosted API (Scale Fast)

**Timeline:** When traffic grows (>1M embeddings/month)  
**Cost:** $50-500/month depending on volume

**Providers:**
- **Voyage AI:** $0.12/1M tokens (logistics-specific models available)
- **Cohere Embed:** $0.10/1M tokens
- **OpenAI Ada-002:** $0.13/1M tokens
- **Jina AI:** $0.02/1M tokens (cheapest)

## Migration Strategy

### Phase 1: POC (Now - 3 months)
```bash
# Use current setup
docker-compose up -d

# Optimize for speed
export Embedding__EmbeddingDimension=256  # or 128
```

**Collect:**
- ✅ Query patterns (what users search for)
- ✅ Relevance feedback (thumbs up/down)
- ✅ Performance metrics (speed, memory)
- ✅ Pain points (where retrieval fails)

### Phase 2: Evaluation (Month 3-4)
```python
# Create test dataset from POC logs
queries = [
    "find shipment BOL-12345",
    "track container TCNU1234567",
    # ... 50-100 real queries
]

# Benchmark candidate models
models = [
    "onnx-community/embeddinggemma-300m-ONNX",  # Current
    "BAAI/bge-base-en-v1.5",                     # Upgrade
    "BAAI/bge-large-en-v1.5"                     # Best
]

# Measure on YOUR data (not MTEB)
for model in models:
    accuracy = evaluate_retrieval(model, queries)
    print(f"{model}: {accuracy:.2%}")
```

### Phase 3: Fine-tuning (Month 4-6)
```bash
# Install fine-tuning tools
pip install sentence-transformers datasets

# Prepare training data
python scripts/prepare_finetuning_data.py \
  --query-logs poc_queries.jsonl \
  --documents logistics_docs/ \
  --output training_data.json

# Fine-tune (takes 2-6 hours on GPU)
python scripts/finetune.py \
  --base-model BAAI/bge-base-en-v1.5 \
  --train-data training_data.json \
  --output models/logistics-embedder-v1

# Convert to ONNX (same as current)
python scripts/convert_to_onnx.py \
  --model models/logistics-embedder-v1 \
  --output models/logistics-embedder-v1.onnx
```

### Phase 4: Re-embedding (Month 6)
```bash
# Update configuration
export Embedding__HuggingFaceRepoId=your-org/logistics-embedder-v1-ONNX
export Embedding__EmbeddingDimension=768  # Back to full dimensions

# Re-embed historical data
python scripts/reembed_all.py \
  --old-embeddings db/embeddings_old/ \
  --new-model models/logistics-embedder-v1.onnx \
  --output db/embeddings_new/ \
  --batch-size 1000

# Zero-downtime migration
# 1. Generate new embeddings in parallel
# 2. Swap database table atomically
# 3. Rollback if issues
```

## Configuration for Easy Migration

### Current appsettings.json (POC)
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma-300m-q4.onnx",
    "EmbeddingDimension": 256,  // POC optimization
    "HuggingFaceRepoId": "onnx-community/embeddinggemma-300m-ONNX",
    "HuggingFaceFilename": "onnx/model_q4.onnx"
  }
}
```

### Future appsettings.json (Production)
```json
{
  "Embedding": {
    // Just change these 3 lines:
    "ModelPath": "/app/models/logistics-embedder-v1.onnx",
    "EmbeddingDimension": 768,
    "HuggingFaceRepoId": "your-org/logistics-embedder-v1-ONNX",
    "HuggingFaceFilename": "model.onnx"
    // Everything else stays the same!
  }
}
```

## Cost Comparison

### POC Phase (Now)
```
Hardware: 4 CPU cores, 8GB RAM          = $20/month (DigitalOcean)
Storage: 50GB (logs + models)           = $5/month
Total:                                  = $25/month
```

### Production Phase (After Fine-tuning)
```
GPU Instance: 1x T4 (for fine-tuning)   = $200 one-time (6 hours)
Fine-tuning Service (alternative)       = $100 (Hugging Face AutoTrain)
Inference: Same CPU as POC              = $20/month
Storage: 100GB                          = $10/month
Total:                                  = $100-200 one-time + $30/month
```

### ROI Calculation
```
Improvement from fine-tuning:
- Retrieval accuracy: 60% → 75% (+25% relative)
- User satisfaction: Fewer "not found" results
- Support tickets: -30% (better search results)
- Developer time saved: $500-1000/month

Break-even: 1-2 months after fine-tuning
```

## Quick Commands

### Start POC (Current)
```bash
cd llm-embedding-api
docker-compose up -d
```

### Monitor POC Metrics
```bash
# Check model size
docker exec llm-embedding-api ls -lh /app/models/

# Test inference speed
curl -X POST http://localhost:8080/api/embedding/generate \
  -H "Content-Type: application/json" \
  -d '{"text": "test query"}' \
  --time-it

# Monitor memory
docker stats llm-embedding-api
```

### Prepare for Migration
```bash
# Export POC query logs
docker exec llm-embedding-api cat /app/logs/queries.log > poc_queries.jsonl

# Count embeddings to re-generate
curl http://localhost:8080/api/stats/embeddings-count
```

## Best Practices

### During POC
1. **Log Everything** - Queries, results, timing, failures
2. **Collect Feedback** - Add thumbs up/down to search results
3. **Monitor Performance** - Track speed, memory, CPU usage
4. **Document Pain Points** - Where does retrieval fail?
5. **Save Examples** - Good and bad search results

### Before Fine-tuning
1. Need 1,000+ query-document pairs (5,000+ ideal)
2. Diverse queries (all use cases covered)
3. Clean labels (relevant/not relevant)
4. Test/train split (80/20)
5. Validation against real use cases

### During Migration
1. **Test in parallel** - Run old and new models side-by-side
2. **A/B testing** - Compare results on sample traffic
3. **Gradual rollout** - 10% → 50% → 100% traffic
4. **Monitoring** - Watch for accuracy/speed regressions
5. **Rollback plan** - Keep old embeddings for 30 days

## Timeline Summary

| Phase | Duration | Investment | Output |
|-------|----------|------------|--------|
| **POC** | 3 months | $75 ($25/mo) | Validated concept, usage metrics |
| **Evaluation** | 1 month | $50 | Benchmark results, model selection |
| **Fine-tuning** | 1 month | $200 | Custom logistics model |
| **Re-embedding** | 1 week | $30 | All data with new embeddings |
| **Total** | ~5 months | **$355** | Production-ready system |

## Recommended Path

```
┌─────────────────┐
│  Now (POC)      │  EmbeddingGemma (256d)
│  Cost: $25/mo   │  Focus: Prove concept works
└────────┬────────┘
         │
         │ 3 months (collect data)
         ↓
┌─────────────────┐
│  Evaluation     │  Test bge-base vs bge-large
│  Cost: $50      │  Focus: Choose best model
└────────┬────────┘
         │
         │ If accuracy is good enough
         ├──→ Use pre-trained bge-large (no fine-tune)
         │
         │ If accuracy needs boost
         ↓
┌─────────────────┐
│  Fine-tuning    │  Custom logistics model
│  Cost: $200     │  Focus: Optimize for YOUR data
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│  Production     │  Deploy fine-tuned model
│  Cost: $30/mo   │  Focus: Scale and monitor
└─────────────────┘
```

## Questions to Answer During POC

1. **Performance:**
   - Is 256d fast enough? (Try 128d if needed)
   - Memory usage acceptable?
   - Batch size optimization?

2. **Accuracy:**
   - Are results relevant?
   - What % of queries fail?
   - Which domains need improvement?

3. **Scale:**
   - Embeddings per day?
   - Storage requirements?
   - Need for caching?

4. **Business:**
   - User satisfaction?
   - Support ticket reduction?
   - ROI of fine-tuning?

## Next Steps

1. ✅ **Start POC** - `docker-compose up -d` (you're already set!)
2. ⬜ **Monitor Usage** - Add logging/analytics
3. ⬜ **Collect Feedback** - Relevance ratings from users
4. ⬜ **Document Pain Points** - What searches fail?
5. ⬜ **Plan Evaluation** - Schedule model comparison in 3 months

---

**Bottom Line:** Your current setup is perfect for POC. Focus on proving value, then invest in fine-tuning when you have budget. The architecture supports easy model swapping—you won't need to rebuild anything.
