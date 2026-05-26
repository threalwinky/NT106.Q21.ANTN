FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/app/shared:/app/main-server

WORKDIR /app

COPY main-server/requirements.txt /tmp/netrix-main-requirements.txt
RUN pip install --no-cache-dir -r /tmp/netrix-main-requirements.txt

COPY shared ./shared
COPY main-server ./main-server

WORKDIR /app/main-server

EXPOSE 8000

CMD ["python", "app.py", "--port", "8000"]
