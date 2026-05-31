FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/app/shared

WORKDIR /app

COPY load-balancer/requirements.txt /tmp/netrix-lb-requirements.txt
RUN pip install --no-cache-dir -r /tmp/netrix-lb-requirements.txt

COPY shared ./shared
COPY load-balancer ./load-balancer

WORKDIR /app/load-balancer

EXPOSE 8002

CMD ["python", "app.py"]
