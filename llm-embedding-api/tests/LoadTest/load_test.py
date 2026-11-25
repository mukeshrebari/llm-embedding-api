#!/usr/bin/env python3
"""
Load Testing Script for LLM Embedding API
Logistics & Transportation Industry Data
Target: 100 transactions per second

Requirements:
    pip install requests aiohttp
"""

import asyncio
import aiohttp
import time
import random
import json
from datetime import datetime, timedelta
from typing import List, Dict
import statistics

# ============================================================================
# Realistic Logistics Data Generators
# ============================================================================

CITIES = [
    "Los Angeles, CA", "Chicago, IL", "Houston, TX", "Phoenix, AZ", 
    "Philadelphia, PA", "San Antonio, TX", "San Diego, CA", "Dallas, TX",
    "San Jose, CA", "Austin, TX", "Jacksonville, FL", "Fort Worth, TX"
]

CARGO_TYPES = [
    "Electronics and Computer Equipment", "Automotive Parts and Accessories",
    "Pharmaceuticals and Medical Supplies", "Perishable Food Products",
    "Industrial Machinery and Tools", "Textiles and Apparel",
    "Chemical Products and Raw Materials", "Construction Materials"
]

CARRIERS = [
    "FedEx Freight", "UPS Freight", "XPO Logistics", "Old Dominion Freight",
    "YRC Worldwide", "Estes Express Lines", "ABF Freight", "SAIA Motor Freight"
]

def generate_trip_data() -> str:
    """Generate realistic shipment trip data"""
    order_num = f"ORD-{random.randint(100000, 999999)}"
    origin = random.choice(CITIES)
    destination = random.choice([c for c in CITIES if c != origin])
    cargo = random.choice(CARGO_TYPES)
    carrier = random.choice(CARRIERS)
    weight = random.randint(500, 45000)
    distance = random.randint(50, 2800)
    rate = round(random.uniform(1.5, 3.5), 2)
    cost = round(distance * rate, 2)
    
    pickup_date = datetime.now() - timedelta(days=random.randint(1, 30))
    delivery_date = pickup_date + timedelta(days=random.randint(1, 7))
    
    return f"""SHIPMENT ORDER: {order_num}
========================================
ORIGIN: {origin}
DESTINATION: {destination}
PICKUP DATE: {pickup_date.strftime('%Y-%m-%d %H:%M')}
DELIVERY DATE: {delivery_date.strftime('%Y-%m-%d %H:%M')}

CARGO DETAILS:
- Type: {cargo}
- Weight: {weight} lbs
- Pallets: {random.randint(1, 26)}
- Declared Value: ${random.randint(5000, 250000)}

CARRIER INFORMATION:
- Carrier: {carrier}
- Driver License: CDL-{random.randint(100000, 999999)}
- Trailer Number: TRL-{random.randint(1000, 9999)}

ROUTE DETAILS:
- Distance: {distance} miles
- Rate per Mile: ${rate}
- Base Freight Charge: ${cost}
- Fuel Surcharge (18%): ${round(cost * 0.18, 2)}
- Total Charge: ${round(cost * 1.18, 2)}

BOL Number: BOL-{random.randint(100000, 999999)}
PO Number: PO-{random.randint(10000, 99999)}
"""

def generate_voucher_data() -> str:
    """Generate realistic accounting voucher data"""
    voucher_num = f"VCH-{datetime.now().strftime('%Y%m%d')}-{random.randint(1000, 9999)}"
    vendor = random.choice(CARRIERS)
    invoice_date = datetime.now() - timedelta(days=random.randint(1, 30))
    due_date = invoice_date + timedelta(days=30)
    
    line_count = random.randint(3, 8)
    total_amount = 0
    line_items = []
    
    for i in range(1, line_count + 1):
        amount = round(random.uniform(100, 5000), 2)
        total_amount += amount
        line_items.append(f"  Line {i}: Freight Charges - Order #{random.randint(100000, 999999)} - ${amount}")
    
    return f"""ACCOUNTING VOUCHER: {voucher_num}
========================================
VOUCHER DATE: {datetime.now().strftime('%Y-%m-%d')}
INVOICE DATE: {invoice_date.strftime('%Y-%m-%d')}
DUE DATE: {due_date.strftime('%Y-%m-%d')}
PAYMENT TERMS: Net 30

VENDOR INFORMATION:
- Vendor Name: {vendor}
- Vendor ID: VND-{random.randint(1000, 9999)}
- Invoice Number: INV-{random.randint(100000, 999999)}

LINE ITEM DETAILS:
{chr(10).join(line_items)}

FINANCIAL SUMMARY:
- Subtotal: ${total_amount:.2f}
- Tax (8%): ${total_amount * 0.08:.2f}
- Total Amount Due: ${total_amount * 1.08:.2f}

Purchase Order: PO-{random.randint(10000, 99999)}
"""

# ============================================================================
# Load Testing Functions
# ============================================================================

class LoadTestResults:
    def __init__(self):
        self.total_requests = 0
        self.successful_requests = 0
        self.failed_requests = 0
        self.response_times = []
        self.errors = []
        self.start_time = None
        self.end_time = None
    
    def add_success(self, response_time: float):
        self.successful_requests += 1
        self.response_times.append(response_time)
    
    def add_failure(self, error: str):
        self.failed_requests += 1
        self.errors.append(error)
    
    def calculate_stats(self) -> Dict:
        duration = (self.end_time - self.start_time).total_seconds()
        
        stats = {
            "total_requests": self.total_requests,
            "successful": self.successful_requests,
            "failed": self.failed_requests,
            "success_rate": (self.successful_requests / self.total_requests * 100) if self.total_requests > 0 else 0,
            "duration_seconds": duration,
            "throughput": self.successful_requests / duration if duration > 0 else 0,
        }
        
        if self.response_times:
            sorted_times = sorted(self.response_times)
            stats.update({
                "avg_response_time": statistics.mean(self.response_times),
                "min_response_time": min(self.response_times),
                "max_response_time": max(self.response_times),
                "median_response_time": statistics.median(self.response_times),
                "p90_response_time": sorted_times[int(len(sorted_times) * 0.90)],
                "p95_response_time": sorted_times[int(len(sorted_times) * 0.95)],
                "p99_response_time": sorted_times[int(len(sorted_times) * 0.99)],
            })
        
        return stats

async def send_embedding_request(
    session: aiohttp.ClientSession,
    url: str,
    text: str,
    request_id: int,
    results: LoadTestResults
):
    """Send a single embedding request"""
    start_time = time.time()
    
    try:
        payload = {"text": text}
        
        async with session.post(
            url,
            json=payload,
            timeout=aiohttp.ClientTimeout(total=30),
            ssl=False  # For local testing with self-signed cert
        ) as response:
            response_data = await response.json()
            
            response_time = (time.time() - start_time) * 1000  # Convert to ms
            
            if response.status == 200:
                results.add_success(response_time)
                embedding_count = len(response_data.get("embedding", []))
                print(f"✓ Request {request_id}: {response_time:.2f}ms - Embedding: {embedding_count}d")
            else:
                error_msg = f"Request {request_id} failed with status {response.status}"
                results.add_failure(error_msg)
                print(f"✗ {error_msg}")
                
    except Exception as e:
        response_time = (time.time() - start_time) * 1000
        error_msg = f"Request {request_id} failed: {str(e)}"
        results.add_failure(error_msg)
        print(f"✗ {error_msg}")

async def run_load_test(
    api_url: str,
    total_requests: int,
    concurrent_requests: int,
    duration_seconds: int
):
    """Execute the load test"""
    
    print("=" * 80)
    print("LOAD TESTING SCRIPT - LLM EMBEDDING API")
    print("Logistics & Transportation Industry Data")
    print("=" * 80)
    print()
    print("Configuration:")
    print(f"  API URL: {api_url}")
    print(f"  Total Requests: {total_requests}")
    print(f"  Concurrent Requests: {concurrent_requests}")
    print(f"  Target Duration: {duration_seconds} seconds")
    print(f"  Target Rate: {total_requests / duration_seconds:.2f} req/sec")
    print()
    
    # Generate test data
    print("Generating test data...")
    test_data = []
    trip_count = int(total_requests * 0.6)  # 60% trip data
    voucher_count = total_requests - trip_count  # 40% voucher data
    
    for _ in range(trip_count):
        test_data.append(generate_trip_data())
    for _ in range(voucher_count):
        test_data.append(generate_voucher_data())
    
    random.shuffle(test_data)
    print(f"Generated {len(test_data)} test records (Trip: {trip_count}, Voucher: {voucher_count})")
    print()
    
    # Initialize results
    results = LoadTestResults()
    results.total_requests = total_requests
    results.start_time = datetime.now()
    
    print("Starting load test...")
    print()
    
    # Create HTTP session
    connector = aiohttp.TCPConnector(limit=concurrent_requests)
    async with aiohttp.ClientSession(connector=connector) as session:
        
        # Create tasks
        tasks = []
        for i in range(total_requests):
            task = send_embedding_request(
                session,
                api_url,
                test_data[i],
                i + 1,
                results
            )
            tasks.append(task)
            
            # Throttle to maintain target rate
            if (i + 1) % concurrent_requests == 0:
                await asyncio.sleep(duration_seconds / (total_requests / concurrent_requests))
        
        # Wait for all tasks to complete
        await asyncio.gather(*tasks)
    
    results.end_time = datetime.now()
    
    # Print results
    print()
    print("=" * 80)
    print("LOAD TEST RESULTS")
    print("=" * 80)
    print()
    
    stats = results.calculate_stats()
    
    print("Summary:")
    print(f"  Total Requests: {stats['total_requests']}")
    print(f"  Successful: {stats['successful']}")
    print(f"  Failed: {stats['failed']}")
    print(f"  Success Rate: {stats['success_rate']:.2f}%")
    print(f"  Duration: {stats['duration_seconds']:.2f} seconds")
    print()
    
    print("Performance Metrics:")
    print(f"  Throughput: {stats['throughput']:.2f} req/sec")
    
    if results.response_times:
        print(f"  Avg Response Time: {stats['avg_response_time']:.2f} ms")
        print(f"  Min Response Time: {stats['min_response_time']:.2f} ms")
        print(f"  Max Response Time: {stats['max_response_time']:.2f} ms")
        print()
        
        print("Response Time Percentiles:")
        print(f"  P50 (Median): {stats['median_response_time']:.2f} ms")
        print(f"  P90: {stats['p90_response_time']:.2f} ms")
        print(f"  P95: {stats['p95_response_time']:.2f} ms")
        print(f"  P99: {stats['p99_response_time']:.2f} ms")
    
    if results.errors:
        print()
        print("Errors (first 10):")
        for error in results.errors[:10]:
            print(f"  {error}")
    
    print()
    print("=" * 80)
    print("INFRASTRUCTURE REQUIREMENTS")
    print("=" * 80)
    print()
    
    # Calculate infrastructure requirements
    target_throughput = 100
    avg_request_memory = 75  # MB
    avg_request_cpu_time = 150  # milliseconds
    model_size = 617  # MB
    
    concurrent_ops = int(target_throughput * (avg_request_cpu_time / 1000))
    memory_for_requests = concurrent_ops * avg_request_memory
    total_memory = model_size + memory_for_requests + 512
    cpu_cores = (concurrent_ops + 1) // 2
    
    print(f"Target: {target_throughput} requests/second")
    print()
    print("Estimated Requirements:")
    print(f"  CPU Cores: {cpu_cores} cores (minimum)")
    print(f"  RAM: {int(total_memory / 1024)} GB")
    print(f"  Concurrent Requests: {concurrent_ops}")
    print(f"  Model Size: {model_size} MB (FP16)")
    print(f"  Memory per Request: ~{avg_request_memory} MB")
    print()
    
    print("Recommended Production Setup:")
    print("  Single Instance:")
    print("    - CPU: 8-16 vCPUs")
    print("    - RAM: 8-12 GB")
    print("    - Expected Throughput: 50-100 req/sec")
    print()
    print("  Load Balanced (3 instances):")
    print("    - 3x instances with 8 vCPUs, 8 GB RAM each")
    print("    - Expected Throughput: 150-300 req/sec")
    print()
    
    print("Load test completed successfully!")

# ============================================================================
# Main Entry Point
# ============================================================================

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="Load test the LLM Embedding API")
    parser.add_argument(
        "--url",
        default="https://localhost:7009/api/embed",
        help="API endpoint URL"
    )
    parser.add_argument(
        "--requests",
        type=int,
        default=1000,
        help="Total number of requests to send"
    )
    parser.add_argument(
        "--concurrent",
        type=int,
        default=100,
        help="Number of concurrent requests"
    )
    parser.add_argument(
        "--duration",
        type=int,
        default=10,
        help="Target duration in seconds"
    )
    
    args = parser.parse_args()
    
    # Run the load test
    asyncio.run(run_load_test(
        api_url=args.url,
        total_requests=args.requests,
        concurrent_requests=args.concurrent,
        duration_seconds=args.duration
    ))
